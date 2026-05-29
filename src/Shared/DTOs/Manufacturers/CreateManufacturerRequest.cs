namespace PBL3.Shared.DTOs.Manufacturers
{
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
}
