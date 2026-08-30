namespace PBL3.Shared.DTOs.Common
{
    /// <summary>
    /// Lớp cơ sở cho mọi request có phân trang.
    /// </summary>
    /// <remarks>
    /// LỚP QUY ƯỚC, ĐI KÈM CHỨ KHÔNG THAY THẾ <c>ClampPageSizeFilter</c> ở tầng API:
    ///   • Filter là lớp bảo vệ THẬT — nó phủ cả request gọi bằng curl, cả endpoint
    ///     nhận tham số rời, và cả endpoint viết sau này.
    ///   • Lớp này nằm ở tầng Shared nên Blazor client DÙNG CHUNG, tức <c>MudTable</c>
    ///     không gửi nổi số lớn ngay từ phía gửi. Giá trị của nó là ở chỗ đó.
    ///
    /// Hai lớp dùng CÙNG một trần (<c>MaxPageSize</c> = 100). Đổi một bên phải đổi bên kia.
    ///
    /// DTO nào chưa kế thừa lớp này vẫn an toàn nhờ filter — đây là quy ước cho code mới.
    /// </remarks>
    public abstract class PagedRequest
    {
        public const int MaxPageSize = 100;
        public const int DefaultPageSize = 10;

        private int _pageNumber = 1;
        private int _pageSize = DefaultPageSize;

        /// <summary>Trang hiện tại, bắt đầu từ 1.</summary>
        public int PageNumber
        {
            get => _pageNumber;
            set => _pageNumber = value < 1 ? 1 : value;
        }

        /// <summary>Số bản ghi mỗi trang, tự chặn trong khoảng [1, 100].</summary>
        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = value < 1 ? 1 : (value > MaxPageSize ? MaxPageSize : value);
        }
    }
}
