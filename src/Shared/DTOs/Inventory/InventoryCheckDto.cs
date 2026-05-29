namespace PBL3.Shared.DTOs.Inventory
{
    public class InventoryCheckDto
    {
        public int Id { get; set; }
        public string CheckCode { get; set; } = string.Empty;
        public Guid EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public DateTime CheckDate { get; set; }
        public DateTime SnapshotAt { get; set; }
        public byte Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public byte ScopeType { get; set; }
        public string ScopeTypeName { get; set; } = string.Empty;
        public int? ScopeCategoryId { get; set; }
        public string? ScopeCategoryName { get; set; }
        public string? Note { get; set; }
        public string? RejectReason { get; set; }
        public Guid? ApprovedByEmployeeId { get; set; }
        public string? ApprovedByEmployeeName { get; set; }
        public DateTime? ApprovedAt { get; set; }

        public List<InventoryCheckDetailLineDto> Details { get; set; } = new();
    }
}
