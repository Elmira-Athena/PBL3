namespace PBL3.Shared.DTOs.Vouchers
{
    /// <summary>
    /// Kết quả kiểm tra voucher — trả về discount preview hoặc thông báo lỗi.
    /// </summary>
    public class ValidateVoucherResponse
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public decimal DiscountAmount { get; set; }
        public string? VoucherName { get; set; }
        public string? Code { get; set; }
    }
}
