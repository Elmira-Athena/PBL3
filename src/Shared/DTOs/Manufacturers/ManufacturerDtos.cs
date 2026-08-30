using PBL3.Shared.DTOs.Common;
namespace PBL3.Shared.DTOs.Manufacturers
{
    // ========================================================
    // READ DTOs
    // ========================================================

    /// <summary>
    /// DTO hiển thị đầy đủ thông tin hãng sản xuất.
    /// </summary>
    public class ManufacturerDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string? Website { get; set; }
        public string? SupportEmail { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    /// <summary>
    /// DTO rút gọn dùng cho Dropdown / Lookup (Id + Name).
    /// </summary>
    public class ManufacturerSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
    }

    // ========================================================
    // WRITE DTOs (CUD)
    // ========================================================

    /// <summary>
    /// Request tạo mới hãng sản xuất.
    /// </summary>
    public class CreateManufacturerRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string? Website { get; set; }
        public string? SupportEmail { get; set; }
    }

    /// <summary>
    /// Request cập nhật hãng sản xuất.
    /// </summary>
    public class UpdateManufacturerRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string? Website { get; set; }
        public string? SupportEmail { get; set; }
    }

    // ========================================================
    // QUERY PARAMETERS
    // ========================================================

    /// <summary>
    /// Bộ lọc cho danh sách hãng sản xuất.
    /// </summary>
    public class ManufacturerFilterRequest : PagedRequest
    {
        /// <summary>
        /// Tìm kiếm theo Tên hoặc Website.
        /// </summary>
        public string? Keyword { get; set; }
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; }
    }
}
