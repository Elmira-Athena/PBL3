namespace PBL3.Shared.DTOs.Analytics
{
    public class InventorySummaryDto
    {
        public int TotalSkus { get; set; }
        public int TotalAvailable { get; set; }
        public int TotalReserved { get; set; }
        public int TotalSold { get; set; }
        public int TotalDefective { get; set; }
        public int TotalReturned { get; set; }
        public int LowStockSkus { get; set; }
    }
}
