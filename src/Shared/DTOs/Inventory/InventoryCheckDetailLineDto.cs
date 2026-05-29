namespace PBL3.Shared.DTOs.Inventory
{
    public class InventoryCheckDetailLineDto
    {
        public int Id { get; set; }
        public int VariantId { get; set; }
        public string VariantName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public int SystemQuantity { get; set; }
        public int ActualQuantity { get; set; }
        public int Difference { get; set; }
        public int MatchedQuantity { get; set; }
        public int MissingQuantity { get; set; }
        public int SurplusQuantity { get; set; }
        public int DefectiveQuantity { get; set; }
        public string? Reason { get; set; }
    }
}
