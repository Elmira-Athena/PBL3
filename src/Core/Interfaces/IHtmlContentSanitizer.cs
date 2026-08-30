namespace PBL3.Core.Interfaces
{
    /// <summary>
    /// Làm sạch HTML do người dùng nhập trước khi lưu xuống DB.
    ///
    /// Áp trên ĐƯỜNG GHI, không phải đường render: vô hiệu hoá dữ liệu ngay tại
    /// chỗ lưu thì mọi consumer hiện tại và tương lai đều an toàn — một chỗ thay
    /// vì mọi chỗ render.
    ///
    /// Mọi trường free-text sẽ được render bằng MarkupString đều PHẢI đi qua đây.
    /// </summary>
    public interface IHtmlContentSanitizer
    {
        /// <summary>
        /// Trả về HTML đã loại bỏ script, handler sự kiện và URI nguy hiểm.
        /// null/rỗng vào thì null/rỗng ra.
        /// </summary>
        string? Sanitize(string? html);
    }
}
