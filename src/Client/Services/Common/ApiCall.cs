using System.Net;
using System.Net.Http.Json;
using PBL3.Shared.DTOs.Common;

namespace Client.Services.Common;

/// <summary>
/// Chỗ duy nhất ánh xạ kết quả HTTP sang thông báo tiếng Việt CÓ TÍNH HÀNH ĐỘNG.
///
/// Vấn đề nó thay thế: mỗi client service tự bắt lỗi theo một kiểu, và kiểu tệ
/// nhất là NUỐT LỖI THÀNH DANH SÁCH RỖNG (OrderClientService trả về
/// new PagedResult&lt;T&gt;() khi request hỏng). Người dùng thấy "bạn chưa có đơn
/// hàng nào" trong khi thật ra là mạng lỗi hoặc token hết hạn. Đó là loại lỗi
/// tệ nhất vì NÓ NÓI DỐI — người dùng không có lý do gì để thử lại.
/// </summary>
public static class ApiCall
{
    /// <summary>
    /// Gọi API và luôn trả về ApiResult&lt;T&gt; — không bao giờ ném, không bao giờ
    /// giả vờ thành công.
    /// </summary>
    /// <param name="send">Hàm thực hiện request.</param>
    /// <param name="action">
    /// Mô tả ngắn việc đang làm, dùng để ghép câu ("tải danh sách đơn hàng").
    /// </param>
    public static async Task<ApiResult<T>> SendAsync<T>(
        Func<Task<HttpResponseMessage>> send, string action)
    {
        HttpResponseMessage response;

        try
        {
            response = await send();
        }
        catch (HttpRequestException)
        {
            return ApiResult<T>.Fail(
                $"Không kết nối được tới máy chủ khi {action}. " +
                "Vui lòng kiểm tra đường truyền rồi thử lại.");
        }
        catch (TaskCanceledException)
        {
            return ApiResult<T>.Fail(
                $"Máy chủ phản hồi quá lâu khi {action}. Vui lòng thử lại.");
        }

        if (response.IsSuccessStatusCode)
        {
            try
            {
                var body = await response.Content.ReadFromJsonAsync<ApiResult<T>>();
                if (body is not null) return body;
            }
            catch
            {
                // rơi xuống câu báo lỗi phía dưới
            }

            return ApiResult<T>.Fail(
                $"Máy chủ trả về dữ liệu không đọc được khi {action}. Vui lòng thử lại.");
        }

        // Ưu tiên thông báo do server soạn — nó biết ngữ cảnh nghiệp vụ rõ hơn ta.
        var serverMessage = await TryReadMessageAsync(response);
        if (!string.IsNullOrWhiteSpace(serverMessage))
        {
            return ApiResult<T>.Fail(serverMessage);
        }

        return ApiResult<T>.Fail(ToUserMessage(response.StatusCode, action));
    }

    /// <summary>
    /// Ánh xạ status code sang câu tiếng Việt nói rõ NGƯỜI DÙNG NÊN LÀM GÌ.
    /// </summary>
    public static string ToUserMessage(HttpStatusCode status, string action) => status switch
    {
        HttpStatusCode.Unauthorized =>
            "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",

        HttpStatusCode.Forbidden =>
            "Bạn không có quyền thực hiện thao tác này.",

        HttpStatusCode.NotFound =>
            $"Không tìm thấy dữ liệu khi {action}. Có thể nó vừa bị xoá.",

        HttpStatusCode.BadRequest =>
            $"Dữ liệu gửi lên không hợp lệ khi {action}. Vui lòng kiểm tra lại.",

        // 409 PHẢI CÓ MẶT TỪ ĐỢT 2, dù hiện chưa endpoint nào trả về nó.
        // Đợt 3 mới bắt đầu ánh xạ DbUpdateConcurrencyException và vi phạm unique
        // index sang 409 — nhưng client phải sẵn sàng TRƯỚC. Ngược lại, ngày đợt 3
        // lên production sẽ là ngày người dùng nhận một thông báo vô nghĩa cho một
        // tình huống hoàn toàn bình thường (hai người sửa cùng một bản ghi).
        HttpStatusCode.Conflict =>
            "Dữ liệu vừa được người khác thay đổi. Vui lòng tải lại trang và thử lại.",

        HttpStatusCode.TooManyRequests =>
            "Bạn thao tác quá nhanh. Vui lòng chờ trong giây lát rồi thử lại.",

        >= HttpStatusCode.InternalServerError =>
            $"Máy chủ đang gặp sự cố khi {action}. Vui lòng thử lại sau ít phút.",

        _ => $"Không thực hiện được thao tác khi {action}. Vui lòng thử lại."
    };

    private static async Task<string?> TryReadMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var result = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
            return result?.Message;
        }
        catch
        {
            return null;
        }
    }
}
