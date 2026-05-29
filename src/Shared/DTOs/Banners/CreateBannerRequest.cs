namespace PBL3.Shared.DTOs.Banners
{
    /// <summary>
    /// Request tạo mới banner.
    /// </summary>
    public class CreateBannerRequest
    {
        public string Title { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string? LinkUrl { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
