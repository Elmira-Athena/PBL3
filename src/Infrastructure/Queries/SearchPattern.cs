namespace PBL3.Infrastructure.Queries
{
    /// <summary>
    /// Dựng mẫu <c>ILIKE</c> cho các ô tìm kiếm "chứa chuỗi", có escape ký tự đại diện.
    /// </summary>
    /// <remarks>
    /// 🎯 <b>Vì sao phải có lớp này thay vì viết <c>$"%{keyword}%"</c> tại chỗ.</b>
    /// <c>string.Contains()</c> được EF Core dịch sang <c>LIKE</c> và <b>tự escape</b> hai ký tự
    /// đại diện <c>%</c> và <c>_</c> giúp. Chuyển sang <c>EF.Functions.ILike</c> là <b>mất</b> phần
    /// escape đó: mẫu do ta tự nối, EF không còn chỗ nào để can thiệp. Hệ quả không phải lỗi mà là
    /// <b>kết quả sai âm thầm</b> — người dùng gõ <c>_</c> (rất thường gặp trong mã hàng) thì nó
    /// thành ký tự đại diện "một ký tự bất kỳ" và trả về thừa; gõ <c>%</c> thì trả về gần như cả bảng.
    /// Không exception, không log, chỉ là danh sách sai.
    ///
    /// Vì vậy escape phải nằm ở <b>một chỗ dùng chung</b>, không rải theo từng call-site: rải ra thì
    /// chỗ tìm kiếm viết sau chỉ cần quên một lần là hỏng, và không có gì bắt được.
    ///
    /// ⚠️ <b>Không dùng cho cột <c>citext</c>.</b> Sáu cột đã đổi sang <c>citext</c>
    /// (<c>SerialNumber</c>, <c>SKU</c>, <c>Code</c>, <c>Slug</c>…) thì <c>.Contains()</c> <b>đã</b>
    /// không phân biệt hoa/thường ở tầng lưu trữ — đổi chúng sang <c>ILike</c> là sửa thứ đang đúng,
    /// và còn làm mất phần escape mà EF vốn lo hộ. Lớp này chỉ dành cho cột <c>text</c>/<c>varchar</c>
    /// thường mà bản SQL Server trước đây đang dựa vào collation CI mặc định của server.
    /// </remarks>
    public static class SearchPattern
    {
        /// <summary>
        /// Ký tự escape truyền tường minh cho <c>ILIKE ... ESCAPE</c>.
        /// </summary>
        /// <remarks>
        /// PostgreSQL mặc định cũng dùng <c>\</c>, nhưng nêu tường minh thì mẫu không phụ thuộc vào
        /// <c>standard_conforming_strings</c> của phiên — một tham số phía server mà tầng ứng dụng
        /// không kiểm soát.
        /// </remarks>
        public const string EscapeCharacter = "\\";

        /// <summary>
        /// Trả về mẫu <c>%từ khoá%</c> đã escape, dùng với
        /// <c>EF.Functions.ILike(cột, mẫu, SearchPattern.EscapeCharacter)</c>.
        /// </summary>
        public static string Contains(string keyword) => $"%{Escape(keyword)}%";

        /// <summary>Vô hiệu hoá <c>\</c>, <c>%</c>, <c>_</c> để chúng được so khớp theo nghĩa đen.</summary>
        /// <remarks>
        /// Thứ tự bắt buộc: <c>\</c> phải thay <b>trước</b>, nếu không hai lượt thay sau sẽ sinh ra
        /// <c>\</c> mới rồi chính chúng bị escape lần nữa.
        /// </remarks>
        private static string Escape(string keyword) => keyword
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
    }
}
