using PBL3.Core.Entities;

namespace PBL3.Core.Interfaces
{
    /// <summary>
    /// Repository interface cho ServiceTicket.
    /// </summary>
    public interface IServiceTicketRepository
    {
        /// <summary>
        /// Lấy danh sách phiếu sửa chữa có phân trang, bộ lọc đa tiêu chí.
        /// </summary>
        Task<(List<ServiceTicket> Items, int TotalCount)> GetPagedListAsync(
            string? keyword,
            byte? status,
            byte? resolutionType,
            Guid? assignedEmployeeId,
            Guid? customerId,
            DateTime? fromDate,
            DateTime? toDate,
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending);

        /// <summary>
        /// Lấy chi tiết phiếu theo Id, không tracking.
        /// </summary>
        Task<ServiceTicket?> GetByIdAsync(int id);

        /// <summary>
        /// Lấy chi tiết phiếu theo Id, bao gồm các related entities (Serial, Order, Customer, StatusHistory, Quotations, RmaShipment, Invoice, ReplacementSerial).
        /// Không tracking (read-only).
        /// </summary>
        Task<ServiceTicket?> GetByIdWithDetailsAsync(int id);

        /// <summary>
        /// Lấy phiếu theo Id với tracking, bao gồm ReplacementSerial, RmaShipment, Quotations.
        /// Dùng cho update operations.
        /// </summary>
        Task<ServiceTicket?> GetByIdWithTrackingAsync(int id);

        /// <summary>
        /// Lấy mã phiếu cuối cùng của một ngày (để sinh mã tự động ST-yyyyMMdd-NNN).
        /// </summary>
        Task<string?> GetLastTicketCodeByDateAsync(string datePrefix);

        /// <summary>
        /// Kiểm tra xem một serial đã có phiếu mở không (status != terminal).
        /// </summary>
        Task<bool> HasOpenTicketForSerialAsync(int serialId);

        /// <summary>
        /// Lấy danh sách phiếu của một customer (theo Order.UserId), với bộ lọc tùy chọn.
        /// </summary>
        Task<(List<ServiceTicket> Items, int TotalCount)> GetTicketsByOrderUserIdAsync(
            Guid userId,
            string? keyword,
            byte? status,
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending);

        Task AddAsync(ServiceTicket ticket);
        Task AddStatusHistoryAsync(ServiceTicketStatusHistory history);
        Task SaveChangesAsync();
    }
}
