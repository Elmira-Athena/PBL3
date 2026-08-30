using Ganss.Xss;
using PBL3.Core.Interfaces;

namespace PBL3.Service.Common
{
    /// <summary>
    /// Làm sạch HTML do người dùng nhập, áp ở TẦNG API TRÊN ĐƯỜNG GHI.
    ///
    /// ═══ VÌ SAO SANITIZE LÚC GHI, KHÔNG PHẢI LÚC RENDER ═══
    ///
    /// Lỗ hổng gốc: ProductDetail.razor render mô tả sản phẩm bằng
    /// @((MarkupString)_product.Description) — MarkupString CỐ Ý bỏ qua HTML
    /// encoding của Razor, nên mọi thẻ &lt;script&gt; trong DB chạy thẳng trên
    /// trình duyệt khách. Kết hợp với việc token nằm trong localStorage thì đó là
    /// một chuỗi tấn công đầy đủ tới refresh token.
    ///
    /// Sanitize lúc GHI vô hiệu hoá dữ liệu NGAY TẠI CHỖ LƯU, nên mọi consumer
    /// hiện tại và tương lai đều an toàn — một chỗ thay vì mọi chỗ render. Nếu
    /// sanitize lúc render thì mỗi màn hình mới lại là một cơ hội quên.
    ///
    /// ═══ VÌ SAO KHÔNG ĐƠN GIẢN BỎ MarkupString ĐI ═══
    ///
    /// Đã kiểm dữ liệu thật trước khi quyết định (2/2 sản phẩm có ký tự '&lt;'):
    /// mô tả đang lưu là HTML THẬT — &lt;h2&gt;, &lt;p&gt;, &lt;img&gt; — chứ
    /// không phải chỉ có xuống dòng. Render text thuần sẽ hiện nguyên các thẻ đó
    /// ra cho khách xem. Vì vậy chọn phương án giữ HTML nhưng làm sạch nó.
    ///
    /// ═══ GIỚI HẠN ĐÃ BIẾT ═══
    ///
    /// Lớp này chỉ bảo vệ dữ liệu GHI TỪ NAY. Dữ liệu đã nằm sẵn trong DB không
    /// tự được làm sạch — muốn chắc thì chạy một lượt UPDATE quét lại toàn bảng.
    /// Với dữ liệu hiện tại (2 sản phẩm, nội dung do seed sinh ra) thì không cần.
    /// </summary>
    public class HtmlContentSanitizer : IHtmlContentSanitizer
    {
        // HtmlSanitizer an toàn để dùng lại nhiều luồng sau khi cấu hình xong,
        // và việc dựng nó khá tốn (kéo theo AngleSharp), nên giữ một bản dùng chung.
        private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

        private static HtmlSanitizer CreateSanitizer()
        {
            var sanitizer = new HtmlSanitizer();

            // Danh sách mặc định của thư viện đã loại <script>, <iframe>, <object>,
            // mọi thuộc tính on* (onerror, onload...) và javascript: URI.
            // Chỉ siết thêm những thứ dự án này thực sự không cần.

            // <style> và thuộc tính style cho phép nhiều đường lách qua CSS
            // (expression(), url(javascript:...)) và không phục vụ nghiệp vụ nào ở đây.
            sanitizer.AllowedTags.Remove("style");
            sanitizer.AllowedAttributes.Remove("style");
            sanitizer.AllowedCssProperties.Clear();

            // Form trong mô tả sản phẩm là dấu hiệu của lừa đảo, không phải nội dung.
            sanitizer.AllowedTags.Remove("form");
            sanitizer.AllowedTags.Remove("input");
            sanitizer.AllowedTags.Remove("button");

            // Ảnh vẫn cho phép (dữ liệu thật đang dùng <img>), nhưng chỉ qua http/https.
            sanitizer.AllowedSchemes.Clear();
            sanitizer.AllowedSchemes.Add("http");
            sanitizer.AllowedSchemes.Add("https");

            // Link ra ngoài luôn kèm rel chống tabnabbing.
            sanitizer.PostProcessNode += (_, e) =>
            {
                if (e.Node is AngleSharp.Html.Dom.IHtmlAnchorElement anchor)
                {
                    anchor.SetAttribute("rel", "noopener noreferrer nofollow");
                }
            };

            return sanitizer;
        }

        public string? Sanitize(string? html)
            => string.IsNullOrWhiteSpace(html) ? html : Sanitizer.Sanitize(html);
    }
}
