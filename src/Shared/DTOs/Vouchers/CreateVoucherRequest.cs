namespace PBL3.Shared.DTOs.Vouchers
{
    /// <summary>
    /// Request tạo mới voucher.
    /// </summary>
    public class CreateVoucherRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public byte DiscountType { get; set; }          // 0: Amount, 1: Percentage
        public decimal DiscountValue { get; set; }
        public decimal MinOrderValue { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int? Quantity { get; set; }              // null = không giới hạn
        public int? MaxUsesPerUser { get; set; }
        public byte ApplyFor { get; set; }              // 0: Both, 1: Online, 2: POS
        public bool IsStackable { get; set; }
        public bool IsActive { get; set; } = true;
        public string? Description { get; set; }
        public List<int>? CategoryIds { get; set; }    // null = áp dụng tất cả danh mục
    }
}
