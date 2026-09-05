using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using PBL3.Infrastructure.Concurrency;
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
    /// ⚠️ <b>Handler này CỐ Ý ở lại mức chung, kể cả sau khi đã có
    /// <c>PostgresException.ConstraintName</c>.</b> Đợt 7 mở ra khả năng tra bảng
    /// "constraint → thông báo" ngay tại đây, và đã <b>không</b> làm. Lý do: câu nói đúng
    /// việc gì vừa hỏng chỉ có <b>service call-site</b> biết — nó biết nó đang tiếp nhận
    /// serial hay đang phê duyệt phiếu kiểm kê. Một bảng tra tập trung ở đây sẽ phải đoán
    /// ngữ cảnh từ tên constraint, và sẽ đoán sai ngay khi một constraint được dùng ở hai
    /// đường nghiệp vụ khác nhau. Vì vậy: <b>call-site lo CÂU CHỮ, handler này lo STATUS
    /// CODE.</b> Hai chỗ dùng <c>ConflictClassifier.IsUniqueViolation(ex, tên)</c> là
    /// <c>ServiceTicketService</c> và <c>InventoryCheckService</c> — chúng <b>dịch nghĩa</b>,
    /// còn đây chỉ <b>xếp loại</b>.
    ///
    /// Và tuyệt đối đừng parse <c>ex.Message</c> để đoán constraint nào: đó là dùng biểu diễn
    /// chuỗi thay cho ngữ nghĩa, đúng cái bẫy #7 — chuỗi ấy tiếng Anh và đổi theo phiên bản.
    ///
    /// 🚨 <b>Việc PHÂN LOẠI không nằm ở file này — nó ở
    /// <see cref="ConflictClassifier"/>, và phải ở đó.</b> Trước kia file này có bản phân loại
    /// RIÊNG, rộng hơn danh sách mà 27 chốt controller cho thoát ra; phần dôi ra
    /// (deadlock — <c>1205</c> thời SQL Server, nay là <c>40P01</c> —
    /// và <c>ConcurrentModificationException</c>) là <b>code chết</b> vì
    /// <c>catch (Exception)</c> ở controller nuốt trước. Hai danh sách trôi khỏi nhau mà không
    /// gì báo. Nay controller hỏi <c>ConflictClassifier.IsConflict</c> và handler hỏi
    /// <c>ConflictClassifier.Classify</c> — <b>cùng một hàm</b>, nên chúng không thể lệch nữa.
    /// Thêm một loại xung đột mới thì sửa đúng một chỗ.
    ///
    /// ⚠️ <b>Không nối <c>ex.Message</c> vào câu trả cho người dùng</b> — chuỗi của EF Core /
    /// SQL Server là tiếng Anh và lộ nội tạng ORM (luật ở <c>CLAUDE.md</c>, chốt
    /// <c>check-error-message-leaks.sh</c>). Chi tiết đi vào <see cref="ILogger{T}"/>.
    /// </remarks>
    public class ConflictExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<ConflictExceptionHandler> _logger;

        public ConflictExceptionHandler(ILogger<ConflictExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            var message = ConflictClassifier.Classify(exception);

            // Trả false = "không phải việc của tôi", để handler tổng xử lý tiếp.
            if (message is null) return false;

            _logger.LogWarning(exception,
                "Xung đột đồng thời ở {Method} {Path} — trả về 409.",
                httpContext.Request.Method, httpContext.Request.Path);

            httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
            httpContext.Response.ContentType = "application/json";

            // ErrorResponseJson.Options bỏ khoá "data" khỏi thân — bắt buộc, nếu không thì
            // 49 chỗ ở client đọc ApiResult<bool> sẽ ném JsonException và nuốt mất câu này.
            // Xem chú thích ở ErrorResponseJson.
            await httpContext.Response.WriteAsync(
                JsonSerializer.Serialize(ApiResult<object>.Fail(message), ErrorResponseJson.Options),
                cancellationToken);

            return true;
        }
    }
}
