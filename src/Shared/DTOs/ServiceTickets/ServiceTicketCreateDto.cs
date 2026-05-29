namespace PBL3.Shared.DTOs.ServiceTickets
{
    public class ServiceTicketCreateDto
    {
        public string SerialNumber { get; set; } = string.Empty;

        public bool HasScratches { get; set; }
        public bool HasDents { get; set; }
        public bool HasBurnMarks { get; set; }
        public bool HasMissingAccessories { get; set; }
        public string? CosmeticNotes { get; set; }

        public string CustomerReportedIssue { get; set; } = string.Empty;

        public string? WalkInCustomerName { get; set; }
        public string? WalkInCustomerPhone { get; set; }
    }
}
