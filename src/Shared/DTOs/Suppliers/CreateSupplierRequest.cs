namespace PBL3.Shared.DTOs.Suppliers
{
    /// <summary>
    /// Request tạo mới nhà cung cấp.
    /// </summary>
    public class CreateSupplierRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? TaxCode { get; set; }
    }
}
