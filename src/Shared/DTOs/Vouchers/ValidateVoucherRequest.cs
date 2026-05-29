namespace PBL3.Shared.DTOs.Vouchers
{
    /// <summary>
    /// Request kiểm tra tính hợp lệ của voucher trước khi áp dụng vào đơn hàng.
    /// </summary>
    public class ValidateVoucherRequest
    {
        public string Code { get; set; } = string.Empty;
        public decimal SubTotal { get; set; }
        public List<int>? OrderItemCategoryIds { get; set; }
        public bool IsOnlineOrder { get; set; } = true;
    }
}
