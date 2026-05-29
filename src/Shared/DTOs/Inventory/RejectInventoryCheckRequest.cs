namespace PBL3.Shared.DTOs.Inventory
{
    public class RejectInventoryCheckRequest
    {
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// true = trả về Draft để quét lại.
        /// false = Cancelled, đóng phiếu.
        /// </summary>
        public bool ReturnToDraft { get; set; }
    }
}
