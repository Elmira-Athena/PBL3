namespace PBL3.Shared.DTOs.Inventory
{
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
