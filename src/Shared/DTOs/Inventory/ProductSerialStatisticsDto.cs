namespace PBL3.Shared.DTOs.Inventory
{
    public class ProductSerialStatisticsDto
    {
        public int TotalCount { get; set; }
        public int AvailableCount { get; set; }
        public int ReservedCount { get; set; }
        public int SoldCount { get; set; }
        public int DefectiveCount { get; set; }
        public int ReturnedCount { get; set; }
        public int LostCount { get; set; }
        public int? ProductId { get; set; }
        public int? VariantId { get; set; }
    }
}
