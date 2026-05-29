namespace PBL3.Shared.DTOs.Products
{
    /// <summary>
    /// Request cập nhật thông tin chung sản phẩm.
    /// </summary>
    public class UpdateProductRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? ShortDescription { get; set; }
        public string? Description { get; set; }
        public int ManufacturerId { get; set; }
        public int CategoryId { get; set; }
        public ProductStatus Status { get; set; }
    }
}
