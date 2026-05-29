namespace PBL3.Shared.DTOs.Products
{
    /// <summary>
    /// Request tạo ảnh cho Variant.
    /// </summary>
    public class SaveImageRequest
    {
        public string ImageUrl { get; set; } = string.Empty;
        public bool IsMain { get; set; }
        public int SortOrder { get; set; }
    }
}
