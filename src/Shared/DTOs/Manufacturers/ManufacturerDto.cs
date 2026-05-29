namespace PBL3.Shared.DTOs.Manufacturers
{
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
}
