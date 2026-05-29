namespace PBL3.Shared.DTOs.Vouchers
{
    /// <summary>
    /// DTO rút gọn dùng cho Dropdown / danh sách nhanh.
    /// </summary>
    public class VoucherSummaryDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public byte DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public bool IsActive { get; set; }
        public DateTime EndDate { get; set; }
    }
}
