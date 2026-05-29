namespace PBL3.Shared.DTOs.Vouchers
{
    /// <summary>Voucher đã được chọn để áp dụng vào đơn hàng.</summary>
    public class AppliedVoucherInfo
    {
        public string Code { get; set; } = string.Empty;
        public decimal DiscountAmount { get; set; }
    }
}
