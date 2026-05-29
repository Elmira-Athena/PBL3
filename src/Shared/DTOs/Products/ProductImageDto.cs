namespace PBL3.Shared.DTOs.Products
{
    /// <summary>
    /// DTO chi tiết một Image.
    /// </summary>
    public class ProductImageDto
    {
        public int Id { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public bool IsMain { get; set; }
        public int SortOrder { get; set; }
    }
}
