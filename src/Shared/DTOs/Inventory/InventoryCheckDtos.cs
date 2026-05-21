namespace PBL3.Shared.DTOs.Inventory
{
    // ========== REQUESTS ==========

    public class CreateInventoryCheckRequest
    {
        /// <summary>0 = AllStore, 1 = Category</summary>
        public byte ScopeType { get; set; }

        /// <summary>Bắt buộc khi ScopeType = 1.</summary>
        public int? ScopeCategoryId { get; set; }

        public string? Note { get; set; }
    }

    public class ScanSerialRequest
    {
        public string SerialNumber { get; set; } = string.Empty;

        /// <summary>
        /// Bắt buộc khi serial không tồn tại trong DB (A3 — UnknownSurplus).
        /// Nhân viên chọn Variant tương ứng để ghi nhận.
        /// </summary>
        public int? VariantIdForUnknown { get; set; }
    }

    public class UpdateScanReasonRequest
    {
        public string Reason { get; set; } = string.Empty;
        public string? ProposedActionNote { get; set; }
    }

    public class RejectInventoryCheckRequest
    {
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// true = trả về Draft để quét lại.
        /// false = Cancelled, đóng phiếu.
        /// </summary>
        public bool ReturnToDraft { get; set; }
    }

    public class InventoryCheckFilterRequest
    {
        public string? Keyword { get; set; }
        public byte? Status { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public Guid? EmployeeId { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; } = true;
    }

    public class InventoryCheckSerialFilterRequest
    {
        public byte? ScanStatus { get; set; }
        public int? VariantId { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    // ========== RESPONSES ==========

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

    public class InventoryCheckDetailLineDto
    {
        public int Id { get; set; }
        public int VariantId { get; set; }
        public string VariantName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public int SystemQuantity { get; set; }
        public int ActualQuantity { get; set; }
        public int Difference { get; set; }
        public int MatchedQuantity { get; set; }
        public int MissingQuantity { get; set; }
        public int SurplusQuantity { get; set; }
        public int DefectiveQuantity { get; set; }
        public string? Reason { get; set; }
    }

    public class InventoryCheckSerialDto
    {
        public int Id { get; set; }
        public int? SerialId { get; set; }
        public string SerialNumberRaw { get; set; } = string.Empty;
        public int? VariantId { get; set; }
        public string? VariantName { get; set; }
        public string? SKU { get; set; }
        public byte? OriginalStatus { get; set; }
        public string? OriginalStatusName { get; set; }
        public byte ScanStatus { get; set; }
        public string ScanStatusName { get; set; } = string.Empty;
        public DateTime? ScannedAt { get; set; }
        public string? Note { get; set; }
        public string? ProposedActionNote { get; set; }
        public bool ResolvedDuringApproval { get; set; }
    }

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

    /// <summary>Kết quả trả về sau mỗi lần quét 1 Serial.</summary>
    public class ScanResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;

        /// <summary>Kết quả phân loại: Matched / Surplus / UnknownSurplus.</summary>
        public byte ScanStatus { get; set; }
        public string ScanStatusName { get; set; } = string.Empty;

        /// <summary>SerialNumber vừa quét.</summary>
        public string SerialNumberRaw { get; set; } = string.Empty;

        /// <summary>true nếu serial đã quét trước đó trong phiếu này.</summary>
        public bool IsDuplicateScan { get; set; }

        /// <summary>true nếu serial không tồn tại DB và cần nhân viên chọn Variant (A3).</summary>
        public bool RequiresVariantInput { get; set; }

        /// <summary>Thông tin serial tìm thấy trong DB (null nếu UnknownSurplus).</summary>
        public string? FoundSerialNumber { get; set; }
        public string? FoundVariantName { get; set; }
        public byte? FoundOriginalStatus { get; set; }
        public string? FoundOriginalStatusName { get; set; }
        public string? SurplusNote { get; set; }

        /// <summary>Mini snapshot tổng hợp counts sau lần scan này.</summary>
        public InventoryCheckDashboardDto? MiniDashboard { get; set; }
    }
}
