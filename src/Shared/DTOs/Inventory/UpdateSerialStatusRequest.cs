namespace PBL3.Shared.DTOs.Inventory
{
    public class UpdateSerialStatusRequest
    {
        public byte NewStatus { get; set; }
        public string? Note { get; set; }
    }
}
