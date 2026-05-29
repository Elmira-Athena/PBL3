namespace PBL3.Shared.DTOs.Inventory
{
    public class UpdateScanReasonRequest
    {
        public string Reason { get; set; } = string.Empty;
        public string? ProposedActionNote { get; set; }
    }
}
