namespace PBL3.Shared.DTOs.Vouchers
{
    /// <summary>
    /// Request cập nhật voucher. Code không thể thay đổi sau khi tạo.
    /// </summary>
    public class UpdateVoucherRequest
    {
        public string Name { get; set; } = string.Empty;
        public byte DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public decimal MinOrderValue { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int? Quantity { get; set; }
        public int? MaxUsesPerUser { get; set; }
        public byte ApplyFor { get; set; }
        public bool IsStackable { get; set; }
        public bool IsActive { get; set; }
        public string? Description { get; set; }
        public List<int>? CategoryIds { get; set; }
    }
}
