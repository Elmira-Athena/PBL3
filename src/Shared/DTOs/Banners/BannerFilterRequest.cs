namespace PBL3.Shared.DTOs.Banners
{
    /// <summary>
    /// Bộ lọc danh sách banner.
    /// </summary>
    public class BannerFilterRequest
    {
        public string? Keyword { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; }
    }
}
