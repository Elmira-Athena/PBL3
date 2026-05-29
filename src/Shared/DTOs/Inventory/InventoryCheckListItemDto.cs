namespace PBL3.Shared.DTOs.Inventory
{
    public class InventoryCheckListItemDto
    {
        public int Id { get; set; }
        public string CheckCode { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public DateTime CheckDate { get; set; }
        public DateTime SnapshotAt { get; set; }
        public byte Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public byte ScopeType { get; set; }
        public string? ScopeCategoryName { get; set; }
        public int TotalVariants { get; set; }
        public string? Note { get; set; }
    }
}
