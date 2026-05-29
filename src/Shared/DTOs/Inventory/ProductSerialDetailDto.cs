namespace PBL3.Shared.DTOs.Inventory
{
    public class ProductSerialDetailDto
    {
        public int Id { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
        public byte Status { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime? SoldDate { get; set; }
        public int VariantId { get; set; }
        public string VariantName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public int WarrantyMonth { get; set; }
        public decimal Price { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int ImportReceiptId { get; set; }
        public string ReceiptCode { get; set; } = string.Empty;
        public DateTime ImportDate { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public int? OrderId { get; set; }
        public string? OrderCode { get; set; }
        public DateTime? OrderDate { get; set; }
        public byte? OrderStatus { get; set; }
    }
}
