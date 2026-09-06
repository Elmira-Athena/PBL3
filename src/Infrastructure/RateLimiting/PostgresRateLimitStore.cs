using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PBL3.Core.RateLimiting;
using PBL3.Infrastructure.Data;

namespace PBL3.Infrastructure.RateLimiting
{
    /// <summary>
    /// Bộ đếm rate limit đặt trong PostgreSQL, dùng chung giữa mọi task API.
    /// </summary>
    /// <remarks>
    /// 🎯 <b>Vì sao PostgreSQL mà không phải Redis.</b> Bốn policy đi qua đây đều là endpoint
    /// nhịp thấp (đăng nhập, đăng ký, refresh, tra cứu) và <b>tự chúng đã phải chạm DB</b> —
    /// nên thêm một câu lệnh nữa không đổi bậc chi phí. Đổi lấy: <b>0 đồng hạ tầng mới</b>,
    /// <b>0 gói NuGet mới</b> (tránh luôn bài học "gói chưa dùng là nợ bảo mật nằm im" của mục
    /// D), và không thêm một thứ nữa phải bật/tắt đúng thứ tự.
    ///
    /// Redis vẫn là lời giải đúng cho <c>ICacheService</c> ở đợt 8; nó chỉ không cần thiết
    /// <i>cho việc này</i>.
    /// </remarks>
    public sealed class PostgresRateLimitStore : IRateLimitStore
    {
        // 🔴 MỘT CÂU LỆNH NGUYÊN TỬ — ĐỪNG TÁCH RA.
        //
        // `INSERT … ON CONFLICT DO UPDATE … RETURNING` tăng bộ đếm VÀ trả về giá trị SAU KHI
        // tăng, trong cùng một lần chạm. Không có khoảng nào giữa "đọc" và "ghi" để một request
        // khác chen vào.
        //
        // Lối viết SAI mà câu này thay thế: SELECT Count → so với hạn mức → UPDATE. Đó là
        // check-then-act; hai request song song cùng đọc ra 4, cùng kết luận "chưa tới 5", cùng
        // ghi 5 ⇒ 6 request đi qua một hạn mức 5, KHÔNG lỗi nào báo ra. Toàn bộ tools/LoadProbe
        // tồn tại để bắt đúng loại này, và S11 đo đúng câu lệnh dưới đây.
        //
        // Dấu nháy kép quanh tên bảng/cột là BẮT BUỘC: PostgreSQL hạ định danh không nháy về
        // chữ thường, còn EF Core tạo bảng đúng PascalCase.
        private const string AcquireSql = """
            INSERT INTO "RateLimitCounters" ("PartitionKey", "WindowStart", "Count")
            VALUES (@key, @windowStart, 1)
            ON CONFLICT ("PartitionKey", "WindowStart")
            DO UPDATE SET "Count" = "RateLimitCounters"."Count" + 1
            RETURNING "Count";
            """;

        // Trần chờ cho CHÍNH câu đếm, không phải cho request. Cố ý rất ngắn: nếu DB chậm tới
        // mức này thì đường đăng nhập đã hỏng vì lý do khác, và bắt người dùng chờ thêm chỉ
        // làm sự cố tệ hơn. Hết hạn ⇒ fail-open + log Warning.
        private const int CommandTimeoutSeconds = 2;

        private readonly HushStoreDbContext _context;
        private readonly ILogger<PostgresRateLimitStore> _logger;

        public PostgresRateLimitStore(
            HushStoreDbContext context, ILogger<PostgresRateLimitStore> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<RateLimitDecision> AcquireAsync(
            string partitionKey,
            int permitLimit,
            TimeSpan window,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var windowStart = FloorToWindow(now, window);
            var retryAfter = (int)Math.Ceiling((windowStart + window - now).TotalSeconds);
            if (retryAfter < 1) retryAfter = 1;

            try
            {
                var count = await ExecuteAcquireAsync(
                    partitionKey, windowStart, cancellationToken);

                return count <= permitLimit
                    ? new RateLimitDecision(true, 0, false)
                    : new RateLimitDecision(false, retryAfter, false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Client tự ngắt — không phải sự cố, đừng log ầm ĩ và đừng fail-open.
                throw;
            }
            catch (Exception ex)
            {
                // 🚨 FAIL-OPEN, CÓ CHỦ Ý, VÀ PHẢI LOG.
                //
                // Lý lẽ: cả bốn endpoint đi qua đây TỰ NÓ đã cần DB (kiểm mật khẩu, ghi
                // AppUsers, tra serial…). DB sập thì chúng hỏng bất kể limiter nói gì, nên
                // fail-closed chỉ thêm một tầng khoá vô ích và biến "DB chậm" thành "không ai
                // đăng nhập được".
                //
                // Nhưng fail-open IM LẶNG thì là tắt rate limit mà không ai biết — nên cờ
                // Degraded đi kèm và chỗ gọi có nghĩa vụ log nó.
                _logger.LogWarning(ex,
                    "Không kiểm được rate limit cho phân vùng {PartitionKey} — FAIL-OPEN, "
                    + "request được cho đi. Rate limit đang KHÔNG có hiệu lực cho đường này.",
                    partitionKey);

                return new RateLimitDecision(true, 0, true);
            }
        }

        private async Task<int> ExecuteAcquireAsync(
            string partitionKey, DateTime windowStart, CancellationToken cancellationToken)
        {
            // Dùng thẳng DbCommand chứ không SqlQueryRaw: EF bọc SqlQueryRaw vào subquery cho
            // composable, và một INSERT không sống nổi trong subquery.
            var connection = _context.Database.GetDbConnection();
            var openedHere = connection.State != ConnectionState.Open;

            if (openedHere)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = AcquireSql;
                command.CommandTimeout = CommandTimeoutSeconds;

                // Chạy NGOÀI transaction nghiệp vụ là đúng: ô đếm phải giữ nguyên kể cả khi
                // giao dịch của request rollback. Filter chạy trước action nên hiện chưa có
                // transaction nào; nếu về sau ai đó mở transaction sớm hơn thì phải xét lại
                // chỗ này, vì rollback sẽ hoàn luôn bộ đếm và hạn mức mất hiệu lực.
                command.Parameters.Add(Param(command, "key", partitionKey));
                command.Parameters.Add(Param(command, "windowStart", windowStart));

                var raw = await command.ExecuteScalarAsync(cancellationToken);
                return Convert.ToInt32(raw);
            }
            finally
            {
                if (openedHere)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private static DbParameter Param(DbCommand command, string name, object value)
        {
            var p = command.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            return p;
        }

        /// <summary>
        /// Làm tròn XUỐNG về mốc đầu cửa sổ (fixed window). Mọi task tính ra cùng một mốc cho
        /// cùng một thời điểm, nên chúng dùng chung đúng một hàng — đó là điều làm bộ đếm này
        /// "dùng chung" thật sự chứ chỉ là ở cùng một bảng.
        /// </summary>
        internal static DateTime FloorToWindow(DateTime utcNow, TimeSpan window)
            => new(utcNow.Ticks / window.Ticks * window.Ticks, DateTimeKind.Utc);
    }
}
