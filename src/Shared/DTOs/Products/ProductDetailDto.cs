namespace PBL3.Shared.DTOs.Products
{
    /// <summary>
    /// DTO chi tiết sản phẩm (dùng cho trang detail).
    /// </summary>
    public class ProductDetailDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? ShortDescription { get; set; }
        public string? Description { get; set; }
        public int ManufacturerId { get; set; }
        public string ManufacturerName { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public ProductStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<ProductVariantDto> Variants { get; set; } = new();
    }
}
