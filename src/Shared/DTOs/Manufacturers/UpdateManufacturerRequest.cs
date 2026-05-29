namespace PBL3.Shared.DTOs.Manufacturers
{
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
}
