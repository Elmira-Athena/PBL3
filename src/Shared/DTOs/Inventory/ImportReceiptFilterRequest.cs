namespace PBL3.Shared.DTOs.Inventory
{
    /// <summary>
    /// Bộ lọc cho danh sách phiếu nhập kho.
    /// </summary>
    public class ImportReceiptFilterRequest
    {
        /// <summary>Tìm kiếm theo Mã phiếu hoặc Tên NCC.</summary>
        public string? Keyword { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int? SupplierId { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; } = true;
    }
}
