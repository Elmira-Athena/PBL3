namespace PBL3.Shared.DTOs.Inventory
{
    public class ScanSerialRequest
    {
        public string SerialNumber { get; set; } = string.Empty;

        /// <summary>
        /// Bắt buộc khi serial không tồn tại trong DB (A3 — UnknownSurplus).
        /// Nhân viên chọn Variant tương ứng để ghi nhận.
        /// </summary>
        public int? VariantIdForUnknown { get; set; }
    }
}
