namespace PBL3.Shared.DTOs.Products
{
    /// <summary>
    /// Request tạo Variant khi tạo Product mới.
    /// </summary>
    public class CreateVariantRequest
    {
        public string SKU { get; set; } = string.Empty;
        public string VariantName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? OriginalPrice { get; set; }
        public int WarrantyMonth { get; set; } = 12;
        public Dictionary<string, string>? Specifications { get; set; }
        public List<SaveImageRequest> Images { get; set; } = new();
    }
}
