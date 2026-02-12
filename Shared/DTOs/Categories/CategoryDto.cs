namespace PBL3.Shared.DTOs.Categories
{
    /// <summary>
    /// DTO chi tiết danh mục (flat, không bao gồm children).
    /// </summary>
    public class CategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public int? ParentId { get; set; }
        public string? ParentName { get; set; }
        public int Level { get; set; }
        public string? ImageUrl { get; set; }
        public int SortOrder { get; set; }
        public bool IsVisible { get; set; }
        public DateTime CreatedDate { get; set; }
    }

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
