namespace PBL3.Shared.DTOs.ServiceTickets
{
    public class ServiceTicketIntakeEvaluationDto
    {
        public string SerialNumber { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string VariantName { get; set; } = string.Empty;

        public DateTime? SoldDate { get; set; }
        public bool IsInWarranty { get; set; }
        public DateTime? WarrantyExpiresOn { get; set; }
        public DateTime? WarrantyEndDate => WarrantyExpiresOn; // Alias for compatibility
        public string WarrantySource { get; set; } = string.Empty; // WarrantyRow, ComputedFromSoldDate, NoWarranty

        public Guid? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }

        public string? BlockingReason { get; set; } // null = can proceed

        public List<string> AllowedBranches { get; set; } = new(); // InternalRepair, Rma, Swap, PaidRepair
    }

    public class ServiceTicketIntakeRequestDto
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

    public class ServiceTicketDetailDto
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

    public class ServiceTicketAssignDto
    {
        public Guid EmployeeId { get; set; }
    }

    public class ServiceTicketDiagnosisDto
    {
        public string DiagnosisFindings { get; set; } = string.Empty;
    }

    public class ServiceTicketBranchDto
    {
        public byte ResolutionType { get; set; } // 1=InternalRepair, 2=Rma, 3=Swap, 4=PaidRepair
    }

    public class ServiceTicketCompleteDto
    {
        public string? Note { get; set; }
    }

    public class ServiceTicketCancelDto
    {
        public string CancelReason { get; set; } = string.Empty;
    }
}
