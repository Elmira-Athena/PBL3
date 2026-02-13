namespace PBL3.Shared.DTOs.Suppliers
{
    // ========================================================
    // READ DTOs
    // ========================================================

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

    // ========================================================
    // WRITE DTOs (CUD)
    // ========================================================

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

    // ========================================================
    // QUERY PARAMETERS
    // ========================================================

    /// <summary>
    /// Bộ lọc cho danh sách nhà cung cấp.
    /// </summary>
    public class SupplierFilterRequest
    {
        /// <summary>
        /// Tìm kiếm theo Tên hoặc Số điện thoại.
        /// </summary>
        public string? Keyword { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; }
    }
}
