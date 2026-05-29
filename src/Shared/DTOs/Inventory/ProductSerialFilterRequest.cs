namespace PBL3.Shared.DTOs.Inventory
{
    public class ProductSerialFilterRequest
    {
        public string? Keyword { get; set; }
        public int? ProductId { get; set; }
        public int? VariantId { get; set; }
        public byte? Status { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; } = true;
    }
}
