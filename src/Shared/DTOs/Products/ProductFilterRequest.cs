namespace PBL3.Shared.DTOs.Products
{
    /// <summary>
    /// Bộ lọc cho danh sách sản phẩm.
    /// </summary>
    public class ProductFilterRequest
    {
        public string? Keyword { get; set; }
        public int? CategoryId { get; set; }
        public int? ManufacturerId { get; set; }
        public decimal? PriceMin { get; set; }
        public decimal? PriceMax { get; set; }
        public ProductStatus? Status { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; }
    }
}
