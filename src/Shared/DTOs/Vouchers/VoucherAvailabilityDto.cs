namespace PBL3.Shared.DTOs.Vouchers
{
    /// <summary>Voucher kèm thông tin có thể áp dụng hay không cho đơn hàng cụ thể.</summary>
    public class VoucherAvailabilityDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public byte DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public decimal MinOrderValue { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsStackable { get; set; }
        public bool IsApplicable { get; set; }
        public decimal EstimatedDiscount { get; set; }
        public string? NotApplicableReason { get; set; }
    }
}
