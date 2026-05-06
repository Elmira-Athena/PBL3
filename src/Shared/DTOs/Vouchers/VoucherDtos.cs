namespace PBL3.Shared.DTOs.Vouchers
{
    // ========================================================
    // READ DTOs
    // ========================================================

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

    // ========================================================
    // WRITE DTOs (CUD)
    // ========================================================

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

    // ========================================================
    // QUERY PARAMETERS
    // ========================================================

    /// <summary>
    /// Bộ lọc cho danh sách voucher.
    /// </summary>
    public class VoucherFilterRequest
    {
        public string? Keyword { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; }
    }

    // ========================================================
    // VALIDATE (preview discount trước checkout)
    // ========================================================

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

    // ========================================================
    // AVAILABLE FOR ORDER (popup chọn voucher ở Checkout)
    // ========================================================

    /// <summary>Request lấy danh sách voucher có thể áp dụng cho đơn hàng hiện tại.</summary>
    public class GetAvailableVouchersRequest
    {
        public decimal SubTotal { get; set; }
        public bool IsOnlineOrder { get; set; } = true;
    }

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

    /// <summary>Voucher đã được chọn để áp dụng vào đơn hàng.</summary>
    public class AppliedVoucherInfo
    {
        public string Code { get; set; } = string.Empty;
        public decimal DiscountAmount { get; set; }
    }
}
