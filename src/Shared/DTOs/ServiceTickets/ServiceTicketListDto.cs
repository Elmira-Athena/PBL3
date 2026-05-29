namespace PBL3.Shared.DTOs.ServiceTickets
{
    public class ServiceTicketListDto
    {
        public int Id { get; set; }
        public string TicketCode { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
        public DateTime IntakeDate { get; set; }
        public byte Status { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        public byte ResolutionType { get; set; }
    }
}
