namespace PBL3.Shared.DTOs.Inventory
{
    public class InventoryCheckSerialDto
    {
        public int Id { get; set; }
        public int? SerialId { get; set; }
        public string SerialNumberRaw { get; set; } = string.Empty;
        public int? VariantId { get; set; }
        public string? VariantName { get; set; }
        public string? SKU { get; set; }
        public byte? OriginalStatus { get; set; }
        public string? OriginalStatusName { get; set; }
        public byte ScanStatus { get; set; }
        public string ScanStatusName { get; set; } = string.Empty;
        public DateTime? ScannedAt { get; set; }
        public string? Note { get; set; }
        public string? ProposedActionNote { get; set; }
        public bool ResolvedDuringApproval { get; set; }
    }
}
