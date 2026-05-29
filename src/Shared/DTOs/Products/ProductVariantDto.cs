namespace PBL3.Shared.DTOs.Products
{
    /// <summary>
    /// DTO chi tiết một Variant (dùng trong ProductDetailDto).
    /// </summary>
    public class ProductVariantDto
    {
        public int Id { get; set; }
        public string SKU { get; set; } = string.Empty;
        public string VariantName { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? OriginalPrice { get; set; }
        public int StockQuantity { get; set; }
        public int WarrantyMonth { get; set; }
        public Dictionary<string, string>? Specifications { get; set; }
        public List<ProductImageDto> Images { get; set; } = new();
    }
}
