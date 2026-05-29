using PBL3.Core.Entities;

namespace PBL3.Core.Interfaces
{
    /// <summary>
    /// Repository interface cho Customer (Quản lý User).
    /// </summary>
    public interface ICustomerRepository
    {
        /// <summary>
        /// Lấy danh sách khách hàng có phân trang và bộ lọc.
        /// </summary>
        Task<(List<AppUser> Items, int TotalCount)> GetPagedListAsync(
            string? keyword,
            bool? isActive,
            byte? gender,
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending);

        /// <summary>
        /// Lấy chi tiết khách hàng và profile.
        /// </summary>
        Task<AppUser?> GetByIdWithProfileAsync(Guid id);

        /// <summary>
        /// Lấy danh sách N đơn hàng mới nhất của người dùng.
        /// </summary>
        Task<List<Order>> GetRecentOrdersAsync(Guid userId, int count);

        /// <summary>
        /// Kiểm tra xem người dùng có đơn hàng nào chưa hoàn tất không.
        /// (0: Pending, 1: Confirmed, 2: Shipping)
        /// </summary>
        Task<bool> HasPendingOrdersAsync(Guid userId);
    }
}
