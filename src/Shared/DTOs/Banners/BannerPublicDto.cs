namespace PBL3.Shared.DTOs.Banners
{
    /// <summary>
    /// DTO rút gọn dùng cho Storefront (trang chủ) — không lộ field IsActive/audit.
    /// </summary>
    public class BannerPublicDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string? LinkUrl { get; set; }
    }
}
