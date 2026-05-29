namespace PBL3.Shared.DTOs.Manufacturers
{
    /// <summary>
    /// Bộ lọc cho danh sách hãng sản xuất.
    /// </summary>
    public class ManufacturerFilterRequest
    {
        /// <summary>
        /// Tìm kiếm theo Tên hoặc Website.
        /// </summary>
        public string? Keyword { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; }
    }
}
