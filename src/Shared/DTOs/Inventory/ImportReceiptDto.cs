namespace PBL3.Shared.DTOs.Inventory
{
    /// <summary>
    /// DTO hiển thị thông tin phiếu nhập kho (danh sách).
    /// </summary>
    public class ImportReceiptDto
    {
        public int Id { get; set; }
        public string ReceiptCode { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public DateTime ImportDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string? Note { get; set; }

        /// <summary>Danh sách chi tiết (chỉ hiển thị ở API chi tiết).</summary>
        public List<ImportReceiptDetailDto>? Details { get; set; }
    }
}
