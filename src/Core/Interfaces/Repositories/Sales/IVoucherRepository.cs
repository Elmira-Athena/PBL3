using PBL3.Core.Entities;

namespace PBL3.Core.Interfaces
{
    /// <summary>
    /// Repository interface cho Voucher &amp; VoucherUsage.
    /// </summary>
    public interface IVoucherRepository
    {
        // ==================== MANAGEMENT CRUD ====================

        /// <summary>
        /// Lấy danh sách voucher phân trang, hỗ trợ lọc theo keyword, trạng thái, date range.
        /// </summary>
        Task<(List<Voucher> Items, int TotalCount)> GetPagedListAsync(
            string? keyword,
            string? statusFilter,
            DateTime? fromDate,
            DateTime? toDate,
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending);

        /// <summary>
        /// Lấy voucher theo Id, bao gồm VoucherCategories. Có tracking để update.
        /// </summary>
        Task<Voucher?> GetByIdWithCategoriesAsync(int id);

        /// <summary>
        /// Lấy voucher theo Id, không tracking. Dùng cho read-only operations.
        /// </summary>
        Task<Voucher?> GetByIdNoTrackingAsync(int id);

        /// <summary>
        /// Kiểm tra mã Code đã tồn tại chưa (excludeId bỏ qua chính nó khi update).
        /// </summary>
        Task<bool> IsDuplicateCodeAsync(string code, int? excludeId = null);

        /// <summary>
        /// Thêm voucher mới vào context.
        /// </summary>
        Task AddAsync(Voucher voucher);

        // ==================== CHECKOUT USAGE ====================

        /// <summary>
        /// Lấy danh sách Voucher theo danh sách mã Code, bao gồm VoucherCategories.
        /// Dùng cho checkout với category restriction check.
        /// </summary>
        Task<List<Voucher>> GetByCodesWithCategoriesAsync(List<string> codes);

        /// <summary>
        /// Lấy danh sách Voucher theo danh sách mã Code (không include categories).
        /// </summary>
        Task<List<Voucher>> GetByCodesAsync(List<string> codes);

        /// <summary>
        /// Đếm số lần user đã dùng mỗi voucher trong danh sách.
        /// Trả về Dictionary(VoucherId → số lần dùng).
        /// Dùng thay GetUsedVoucherIdsByUserAsync để hỗ trợ MaxUsesPerUser.
        /// </summary>
        Task<Dictionary<int, int>> GetUserVoucherUsageCountsAsync(Guid userId, List<int> voucherIds);

        /// <summary>
        /// Kiểm tra danh sách cặp (UserId, VoucherId) đã tồn tại trong VoucherUsages chưa.
        /// Trả về danh sách VoucherId mà User này đã dùng.
        /// </summary>
        Task<List<int>> GetUsedVoucherIdsByUserAsync(Guid userId, List<int> voucherIds);

        /// <summary>
        /// Thêm danh sách VoucherUsage vào context.
        /// </summary>
        Task AddUsagesAsync(IEnumerable<VoucherUsage> usages);

        /// <summary>
        /// Lấy tất cả voucher active, trong thời hạn hiệu lực, chưa hết số lượng.
        /// Dùng cho popup chọn voucher ở trang Checkout.
        /// </summary>
        Task<List<Voucher>> GetActiveVouchersForCustomerAsync();

        Task SaveChangesAsync();
    }
}
