using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PBL3.Core.Exceptions;
using PBL3.Shared.DTOs.Common;

namespace PBL3.API.Middleware
{
    /// <summary>
    /// Ánh xạ các lỗi "hai người đụng nhau" sang <b>HTTP 409 Conflict</b> kèm câu tiếng Việt,
    /// thay vì để chúng rơi xuống handler tổng và thành <b>500</b>.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>VÌ SAO 409 CHỨ KHÔNG PHẢI 500 — đây là toàn bộ lý do tồn tại của file này.</b>
    /// <c>500</c> nói "server hỏng": người dùng không biết làm gì, và bấm lại thường vô nghĩa.
    /// <c>409</c> nói "bạn thử lại đi": tình huống hai người sửa cùng một bản ghi là chuyện
    /// <em>hoàn toàn bình thường</em>, không phải sự cố. Client đã sẵn sàng từ <b>đợt 2</b> —
    /// <c>ApiCall.ToUserMessage</c> có ánh xạ <c>409 → "Dữ liệu vừa được người khác thay đổi.
    /// Vui lòng tải lại trang và thử lại."</c> nằm đó từ trước khi có endpoint nào trả về nó,
    /// đúng vì lý do này.
    ///
    /// ⚠️ <b>THỨ TỰ ĐĂNG KÝ QUAN TRỌNG HƠN NỘI DUNG.</b> Handler này phải được
    /// <c>UseExceptionHandler</c> gọi <b>TRƯỚC</b> handler tổng ở <c>Program.cs</c>. Đăng ký
    /// sau thì handler tổng đã ghi <c>500</c> và kết thúc response — file này thành code chết
    /// mà không có gì báo lỗi.
    ///
    /// ⚠️ <b>Ở đây SQL Server thua PostgreSQL một bậc.</b> <see cref="SqlException"/>
    /// <b>không có</b> thuộc tính tên constraint, nên không tra bảng "constraint → thông báo"
    /// sạch sẽ được. Cách đúng trên SQL Server là <b>để service call-site cung cấp thông báo
    /// theo ngữ cảnh</b> — nó biết nó đang làm gì — còn handler này chỉ lo <b>status code</b>.
    /// Đừng parse <c>ex.Message</c> để đoán constraint nào: đó là dùng biểu diễn chuỗi thay
    /// cho ngữ nghĩa, đúng cái bẫy #7. Sau khi chuyển PostgreSQL (đợt 7) thì đổi sang tra
    /// theo <c>PostgresException.ConstraintName</c>, sạch hơn.
    ///
    /// ⚠️ <b>Không nối <c>ex.Message</c> vào câu trả cho người dùng</b> — chuỗi của EF Core /
    /// SQL Server là tiếng Anh và lộ nội tạng ORM (luật ở <c>CLAUDE.md</c>, chốt
    /// <c>check-error-message-leaks.sh</c>). Chi tiết đi vào <see cref="ILogger{T}"/>.
    /// </remarks>
    public class ConflictExceptionHandler : IExceptionHandler
    {
        /// <summary>Vi phạm unique index (2601 = unique index, 2627 = unique constraint).</summary>
        private const int UniqueIndexViolation = 2601;
        private const int UniqueConstraintViolation = 2627;

        /// <summary>Deadlock — SQL Server đã chọn giao dịch này làm nạn nhân.</summary>
        private const int DeadlockVictim = 1205;

        private readonly ILogger<ConflictExceptionHandler> _logger;

        public ConflictExceptionHandler(ILogger<ConflictExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            var message = Classify(exception);

            // Trả false = "không phải việc của tôi", để handler tổng xử lý tiếp.
            if (message is null) return false;

            _logger.LogWarning(exception,
                "Xung đột đồng thời ở {Method} {Path} — trả về 409.",
                httpContext.Request.Method, httpContext.Request.Path);

            httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
            httpContext.Response.ContentType = "application/json";

            await httpContext.Response.WriteAsync(
                JsonSerializer.Serialize(
                    ApiResult<object>.Fail(message),
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                cancellationToken);

            return true;
        }

        /// <summary>
        /// Trả về câu tiếng Việt nếu đây là xung đột đồng thời, <c>null</c> nếu không phải.
        /// </summary>
        private static string? Classify(Exception exception) => exception switch
        {
            // Chốt chống race BÊN TRONG transaction đã tự phát hiện và tự soạn thông báo
            // tiếng Việt an toàn để hiển thị — dùng nguyên văn, đừng thay bằng câu chung.
            ConcurrentModificationException ex => ex.Message,

            // RowVersion khớp 0 dòng => ai đó vừa sửa bản ghi. Đợt 3 phần 2 mới đặt
            // RowVersion lên 6 entity, nhưng ánh xạ phải có mặt TRƯỚC để ngày bật token
            // không phải ngày người dùng nhận 500 cho một tình huống bình thường.
            DbUpdateConcurrencyException =>
                "Dữ liệu vừa được người khác thay đổi. Vui lòng tải lại trang và thử lại.",

            DbUpdateException { InnerException: SqlException sql } => FromSqlError(sql.Number),

            SqlException sql => FromSqlError(sql.Number),

            _ => null
        };

        private static string? FromSqlError(int number) => number switch
        {
            UniqueIndexViolation or UniqueConstraintViolation =>
                "Dữ liệu này vừa được người khác tạo hoặc thay đổi. Vui lòng tải lại trang và thử lại.",

            DeadlockVictim =>
                "Hệ thống đang bận, vui lòng thử lại sau giây lát.",

            _ => null
        };
    }
}
