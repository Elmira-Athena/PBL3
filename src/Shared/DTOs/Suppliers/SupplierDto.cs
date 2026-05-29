namespace PBL3.Shared.DTOs.Suppliers
{
    /// <summary>
    /// DTO hiển thị thông tin nhà cung cấp.
    /// </summary>
    public class SupplierDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? TaxCode { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
