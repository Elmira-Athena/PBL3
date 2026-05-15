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

    /// <summary>
    /// Request cập nhật banner. Schema giống Create — kế thừa để chia sẻ validator.
    /// </summary>
    public class UpdateBannerRequest : CreateBannerRequest
    {
    }

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
