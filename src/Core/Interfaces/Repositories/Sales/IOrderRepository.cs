using PBL3.Core.Entities;

namespace PBL3.Core.Interfaces
{
    /// <summary>
    /// Repository interface cho Order.
    /// </summary>
    public interface IOrderRepository
    {
        IQueryable<Order> GetQueryable();
        Task<Order?> GetByIdAsync(int id);

        /// <summary>
        /// Lấy chi tiết đơn hàng theo Id, bao gồm Details và Serials (AsNoTracking — dùng cho read-only).
        /// </summary>
        Task<Order?> GetByIdWithDetailsAsync(int id);

        /// <summary>
        /// Lấy chi tiết đơn hàng theo Id kèm tracking (dùng cho các thao tác ghi: xuất kho, cập nhật trạng thái...).
        /// </summary>
        Task<Order?> GetByIdWithDetailsTrackedAsync(int id);

        Task<string?> GetLastOrderCodeByDateAsync(string datePrefix);

        /// <summary>
        /// Lấy danh sách các đơn POS đang lưu nháp bởi một nhân viên.
        /// </summary>
        Task<List<Order>> GetDraftsByEmployeeAsync(Guid employeeId);

        /// <summary>
        /// Tính tổng Quantity đã đặt theo VariantId cho các đơn Active (Status 0 hoặc 1).
        /// Dùng cho Virtual Inventory Hold — 1 query batch, không N+1.
        /// </summary>
        Task<Dictionary<int, int>> GetActiveOrderQuantitiesByVariantIdsAsync(List<int> variantIds);

        Task AddAsync(Order order);
        Task SaveChangesAsync();
    }
}
