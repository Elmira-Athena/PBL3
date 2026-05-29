namespace PBL3.Shared.DTOs.Categories
{
    /// <summary>
    /// Request cập nhật danh mục.
    /// </summary>
    public class UpdateCategoryRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public int? ParentId { get; set; }
        public string? ImageUrl { get; set; }
        public int SortOrder { get; set; }
        public bool IsVisible { get; set; } = true;
    }
}
