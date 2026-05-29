namespace PBL3.Shared.DTOs.Products
{
    /// <summary>
    /// Request tạo sản phẩm mới (bao gồm Variants lồng nhau).
    /// </summary>
    public class CreateProductRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? ShortDescription { get; set; }
        public string? Description { get; set; }
        public int ManufacturerId { get; set; }
        public int CategoryId { get; set; }
        public List<CreateVariantRequest> Variants { get; set; } = new();
    }
}
