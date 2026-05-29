namespace PBL3.Shared.DTOs.ServiceTickets
{
    public class ServiceTicketDetailDto
    {
        public int Id { get; set; }
        public string TicketCode { get; set; } = string.Empty;

        public string SerialNumber { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int SerialVariantId { get; set; }
        public string? CustomerName { get; set; }

        public DateTime IntakeDate { get; set; }
        public byte Status { get; set; }
        public string StatusLabel { get; set; } = string.Empty;

        public byte ResolutionType { get; set; }

        public bool HasScratches { get; set; }
        public bool HasDents { get; set; }
        public bool HasBurnMarks { get; set; }
        public bool HasMissingAccessories { get; set; }
        public string? CosmeticNotes { get; set; }

        public bool WasInWarrantyAtIntake { get; set; }
        public DateTime? WarrantyEndDateAtIntake { get; set; }

        public string CustomerReportedIssue { get; set; } = string.Empty;

        public string? DiagnosisFindings { get; set; }
        public DateTime? DiagnosedAt { get; set; }

        public Guid? AssignedEmployeeId { get; set; }
        public string? AssignedEmployeeName { get; set; }

        public List<ServiceTicketStatusHistoryDto> StatusHistory { get; set; } = new();
        public List<QuotationDetailDto> Quotations { get; set; } = new();
        public RmaShipmentDetailDto? RmaShipment { get; set; }
        public ServiceInvoiceDetailDto? Invoice { get; set; }
    }
}
