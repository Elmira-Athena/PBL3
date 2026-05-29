namespace PBL3.Shared.DTOs.ServiceTickets
{
    public class QuotationAcceptDto
    {
        public byte NextStatus { get; set; } // 4=WaitingParts, 5=InRepair
    }
}
