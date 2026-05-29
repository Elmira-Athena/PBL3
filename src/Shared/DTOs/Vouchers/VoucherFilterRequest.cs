namespace PBL3.Shared.DTOs.Vouchers
{
    /// <summary>
    /// Bộ lọc cho danh sách voucher.
    /// StatusFilter: null/""=Tất cả, "active"=Đang hoạt động, "upcoming"=Sắp diễn ra,
    ///               "expired"=Hết hạn, "exhausted"=Hết lượt dùng, "paused"=Tạm dừng
    /// </summary>
    public class VoucherFilterRequest
    {
        public string? Keyword { get; set; }
        public string? StatusFilter { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; }
    }
}
