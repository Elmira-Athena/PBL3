namespace PBL3.Shared.DTOs.Inventory
{
    public class InventoryCheckDashboardDto
    {
        public int CheckId { get; set; }
        public string CheckCode { get; set; } = string.Empty;
        public byte Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public DateTime SnapshotAt { get; set; }

        public int TotalSystem { get; set; }
        public int TotalScanned { get; set; }
        public int MatchedCount { get; set; }
        public int MissingCount { get; set; }
        public int SurplusCount { get; set; }
        public int UnknownSurplusCount { get; set; }
        public int DefectiveCount { get; set; }

        /// <summary>Phần trăm đã quét = TotalScanned / TotalSystem * 100.</summary>
        public decimal PercentComplete => TotalSystem > 0
            ? Math.Round((decimal)TotalScanned / TotalSystem * 100, 1)
            : 0;
    }
}
