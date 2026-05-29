namespace PBL3.Shared.DTOs.Vouchers
{
    /// <summary>Request lấy danh sách voucher có thể áp dụng cho đơn hàng hiện tại.</summary>
    public class GetAvailableVouchersRequest
    {
        public decimal SubTotal { get; set; }
        public bool IsOnlineOrder { get; set; } = true;
    }
}
