namespace PBL3.Shared.DTOs.Suppliers
{
    /// <summary>
    /// Bộ lọc cho danh sách nhà cung cấp.
    /// </summary>
    public class SupplierFilterRequest
    {
        /// <summary>
        /// Tìm kiếm theo Tên hoặc Số điện thoại.
        /// </summary>
        public string? Keyword { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; }
    }
}
