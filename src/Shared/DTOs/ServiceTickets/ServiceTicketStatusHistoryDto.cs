namespace PBL3.Shared.DTOs.ServiceTickets
{
    public class ServiceTicketStatusHistoryDto
    {
        public int Id { get; set; }
        public byte FromStatus { get; set; }
        public string FromStatusLabel { get; set; } = string.Empty;
        public byte ToStatus { get; set; }
        public string ToStatusLabel { get; set; } = string.Empty;
        public DateTime ChangedAt { get; set; }
        public string? Note { get; set; }
    }
}
