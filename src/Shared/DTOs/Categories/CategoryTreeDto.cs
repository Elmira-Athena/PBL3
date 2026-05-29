namespace PBL3.Shared.DTOs.Categories
{
    /// <summary>
    /// DTO recursive dùng cho Tree View / Mega Menu.
    /// </summary>
    public class CategoryTreeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public int? ParentId { get; set; }
        public int Level { get; set; }
        public string? ImageUrl { get; set; }
        public int SortOrder { get; set; }
        public bool IsVisible { get; set; }
        public List<CategoryTreeDto> Children { get; set; } = new();
    }
}
