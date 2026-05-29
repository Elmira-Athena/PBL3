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
}
