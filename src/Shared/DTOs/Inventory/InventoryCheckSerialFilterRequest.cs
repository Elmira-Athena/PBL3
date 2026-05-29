namespace PBL3.Shared.DTOs.Inventory
{
    public class InventoryCheckSerialFilterRequest
    {
        public byte? ScanStatus { get; set; }
        public int? VariantId { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
