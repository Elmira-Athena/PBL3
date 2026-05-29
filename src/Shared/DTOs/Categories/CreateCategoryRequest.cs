namespace PBL3.Shared.DTOs.Categories
{
    /// <summary>
    /// Request tạo mới danh mục.
    /// </summary>
    public class CreateCategoryRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public int? ParentId { get; set; }
        public string? ImageUrl { get; set; }
        public int SortOrder { get; set; }
        public bool IsVisible { get; set; } = true;
    }
}
