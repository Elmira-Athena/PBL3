namespace PBL3.Shared.DTOs.Suppliers
{
    /// <summary>
    /// Request cập nhật nhà cung cấp.
    /// </summary>
    public class UpdateSupplierRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? TaxCode { get; set; }
    }
}
