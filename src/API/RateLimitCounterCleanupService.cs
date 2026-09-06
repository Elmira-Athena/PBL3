using Microsoft.EntityFrameworkCore;
using PBL3.Infrastructure.Data;

namespace PBL3.API
{
    /// <summary>
    /// Dọn các hàng <c>RateLimitCounters</c> đã hết hạn.
    /// </summary>
    /// <remarks>
    /// 🎯 <b>Vì sao cần dọn.</b> Mỗi (policy, IP, cửa sổ) sinh một hàng và <b>không bao giờ tự
    /// mất</b>. Cửa sổ dài nhất là 1 giờ (<c>RegisterRateLimit</c>), nên bảng cứ lớn mãi theo
    /// số IP từng ghé. Không phải vấn đề tuần đầu, là vấn đề tháng thứ ba — đúng loại nợ im
    /// lặng mà repo này hay bị.
    ///
    /// 🎯 <b>Vì sao là background job mà không dọn ngay trong đường request.</b> Luật
    /// "Async vs Sync Decision" của CLAUDE.md: chỉ những gì cần nhất quán ngay mới được chặn
    /// luồng chính. Thêm một <c>DELETE</c> vào mỗi lượt đăng nhập là trả giá trên đường nóng
    /// cho một việc chẳng ai chờ.
    ///
    /// ⚠️ <b>Chạy trên MỌI task, và như vậy là ĐÚNG.</b> Câu <c>DELETE</c> idempotent nên N
    /// task cùng dọn không sinh lỗi — cùng lắm là một task xoá trước, task sau xoá 0 hàng. Cố
    /// tình KHÔNG thêm khoá phân tán: bài toán không xứng độ phức tạp đó.
    ///
    /// 🚨 <b>Không được xoá hàng của cửa sổ ĐANG mở.</b> Xoá là đặt bộ đếm về 0 giữa cửa sổ,
    /// tức tự tay nới hạn mức. Vì vậy mốc xoá là <c>now - RetentionPeriod</c> với
    /// <c>RetentionPeriod</c> <b>lớn hơn hẳn</b> cửa sổ dài nhất, không phải bằng nó.
    /// </remarks>
    public sealed class RateLimitCounterCleanupService : BackgroundService
    {
        /// <summary>Cửa sổ dài nhất hiện tại là 1 giờ; giữ 3 giờ cho biên an toàn rộng.</summary>
        private static readonly TimeSpan RetentionPeriod = TimeSpan.FromHours(3);

        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<RateLimitCounterCleanupService> _logger;

        public RateLimitCounterCleanupService(
            IServiceScopeFactory scopeFactory,
            ILogger<RateLimitCounterCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Chờ một nhịp trước lần dọn đầu: lúc app vừa lên là lúc nó đang chạy migration,
            // đăng ký service, kết nối DB — đừng thêm việc vào đúng khoảnh khắc đó.
            using var timer = new PeriodicTimer(Interval);

            while (await SafeWaitAsync(timer, stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var context = scope.ServiceProvider
                        .GetRequiredService<HushStoreDbContext>();

                    var cutoff = DateTime.UtcNow - RetentionPeriod;

                    // ExecuteDeleteAsync: một câu DELETE, không kéo hàng nào về RAM.
                    // An toàn với concurrency token vì bảng này KHÔNG có token nào —
                    // cảnh báo "ExecuteUpdate bỏ qua token" của CLAUDE.md không áp dụng.
                    var removed = await context.RateLimitCounters
                        .Where(c => c.WindowStart < cutoff)
                        .ExecuteDeleteAsync(stoppingToken);

                    if (removed > 0)
                    {
                        _logger.LogInformation(
                            "Đã dọn {Removed} hàng đếm rate limit hết hạn.", removed);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Dọn rác thất bại KHÔNG được làm app chết. Bảng phình là vấn đề của
                    // tuần sau; một BackgroundService ném ra ngoài thì host tắt NGAY.
                    _logger.LogWarning(ex,
                        "Dọn hàng đếm rate limit thất bại. Sẽ thử lại sau {Interval}.",
                        Interval);
                }
            }
        }

        private static async Task<bool> SafeWaitAsync(
            PeriodicTimer timer, CancellationToken stoppingToken)
        {
            try
            {
                return await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
    }
}
