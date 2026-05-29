namespace PBL3.Shared.DTOs.Banners
{
    /// <summary>
    /// DTO đầy đủ thông tin banner (dùng cho Admin).
    /// </summary>
    public class BannerDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string? LinkUrl { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
