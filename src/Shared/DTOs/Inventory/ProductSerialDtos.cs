namespace PBL3.Shared.DTOs.Inventory
{
    public class ProductSerialFilterRequest
    {
        public string? Keyword { get; set; }
        public int? ProductId { get; set; }
        public int? VariantId { get; set; }
        public byte? Status { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; } = true;
    }

    public class ProductSerialListDto
    {
        public int Id { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
        public int VariantId { get; set; }
        public string VariantName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int ImportReceiptId { get; set; }
        public string ReceiptCode { get; set; } = string.Empty;
        public byte Status { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        public int? OrderId { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? SoldDate { get; set; }
    }

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

    public class ProductSerialStatisticsDto
    {
        public int TotalCount { get; set; }
        public int AvailableCount { get; set; }
        public int ReservedCount { get; set; }
        public int SoldCount { get; set; }
        public int DefectiveCount { get; set; }
        public int ReturnedCount { get; set; }
        public int? ProductId { get; set; }
        public int? VariantId { get; set; }
    }

    public class UpdateSerialStatusRequest
    {
        public byte NewStatus { get; set; }
        public string? Note { get; set; }
    }
}
