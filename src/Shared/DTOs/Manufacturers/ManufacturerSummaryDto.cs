namespace PBL3.Shared.DTOs.Manufacturers
{
    /// <summary>
    /// DTO rút gọn dùng cho Dropdown / Lookup (Id + Name).
    /// </summary>
    public class ManufacturerSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
    }
}
