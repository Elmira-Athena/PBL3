namespace PBL3.Shared.DTOs.Vouchers
{
    /// <summary>
    /// DTO hiển thị đầy đủ thông tin voucher (dùng cho trang chi tiết và danh sách quản trị).
    /// </summary>
    public class VoucherDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public byte DiscountType { get; set; }          // 0: Giảm tiền cố định, 1: Giảm theo %
        public decimal DiscountValue { get; set; }
        public decimal MinOrderValue { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int? Quantity { get; set; }              // null = không giới hạn
        public int UsedCount { get; set; }
        public int? MaxUsesPerUser { get; set; }        // null = không giới hạn lần dùng/người
        public byte ApplyFor { get; set; }              // 0: Cả hai, 1: Online, 2: POS
        public bool IsStackable { get; set; }
        public bool IsActive { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<int> CategoryIds { get; set; } = new();
    }
}
