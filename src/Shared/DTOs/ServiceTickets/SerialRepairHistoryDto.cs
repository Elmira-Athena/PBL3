namespace PBL3.Shared.DTOs.ServiceTickets
{
    public class SerialRepairHistoryDto
    {
        public int Id { get; set; }
        public int SerialId { get; set; }
        public int? TicketId { get; set; }
        public string? TicketCode { get; set; }

        public byte ResolutionType { get; set; }
        public string ResolutionTypeLabel { get; set; } = string.Empty;

        public DateTime LoggedAt { get; set; }
        public string Summary { get; set; } = string.Empty;

        public int? ReplacedBySerialId { get; set; }
        public string? ReplacedBySerialNumber { get; set; }
    }
}
