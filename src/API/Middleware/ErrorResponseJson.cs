using System.Text.Json;
using System.Text.Json.Serialization;

namespace PBL3.API.Middleware
{
    /// <summary>
    /// Tuỳ chọn JSON dùng chung cho MỌI phản hồi lỗi cắt ngang (429, 500, 403 khoá tài khoản,
    /// 409 xung đột).
    /// </summary>
    /// <remarks>
    /// 🚨 <b><c>DefaultIgnoreCondition = WhenWritingNull</c> KHÔNG phải chuyện thẩm mỹ — nó là
    /// bản vá cho một lỗi làm 49 chỗ ở tầng Client không đọc nổi thông báo lỗi.</b>
    ///
    /// Bốn đường lỗi cắt ngang đều ghi <c>ApiResult&lt;object&gt;.Fail(...)</c>, tức thân phản hồi
    /// có <c>"data": null</c>. Nhưng client đọc chúng bằng <c>ReadFromJsonAsync&lt;ApiResult&lt;bool&gt;&gt;()</c>
    /// ở <b>49 call-site</b> (47 <c>bool</c> + 2 <c>int</c>, trong 18 file).
    ///
    /// <c>ApiResult&lt;T&gt;.Data</c> khai là <c>T?</c>, nhưng <c>T</c> KHÔNG ràng buộc nên <c>T?</c>
    /// chỉ là chú thích nullable — ở runtime với <c>T = bool</c> nó vẫn là <c>bool</c> không-nullable,
    /// và <c>System.Text.Json</c> NÉM khi gán <c>null</c> vào đó. Đã đo:
    /// <code>
    /// ApiResult&lt;bool&gt;   -> JsonException: The JSON value could not be converted to System.Boolean
    /// ApiResult&lt;int&gt;    -> JsonException
    /// ApiResult&lt;string&gt; -> OK   (kiểu tham chiếu nhận null bình thường)
    /// </code>
    /// Hậu quả: client rơi vào <c>catch (Exception)</c> và thay câu tiếng Việt server soạn bằng
    /// câu chung chung. Toàn bộ chuỗi 27 chốt controller → <see cref="ConflictExceptionHandler"/>
    /// → <c>RowVersion</c> → unique index của đợt 3 dừng lại ở đúng bước cuối này. Và nó không chỉ
    /// giết 409: <b>403 "tài khoản bị khoá" cũng chết</b> — người dùng không thấy được lý do khoá.
    ///
    /// Bỏ hẳn khoá <c>data</c> khỏi thân phản hồi lỗi thì <c>bool</c> nhận <c>default(false)</c> và
    /// không ném. Sửa ở server là 4 chỗ và đóng vĩnh viễn; sửa ở client là 49 chỗ và mọi client viết
    /// sau phải nhớ. Ngoài ra <c>data: null</c> vốn không mang thông tin gì trong một phản hồi lỗi.
    ///
    /// ⚠️ Chỉ dùng cho phản hồi LỖI. Đường thành công phải giữ <c>data</c> kể cả khi null, vì ở đó
    /// "null" là một câu trả lời có nghĩa (không tìm thấy, danh sách rỗng…).
    /// </remarks>
    public static class ErrorResponseJson
    {
        public static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }
}
