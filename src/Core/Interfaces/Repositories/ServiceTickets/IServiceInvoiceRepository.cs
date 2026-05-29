using PBL3.Core.Entities;

namespace PBL3.Core.Interfaces
{
    /// <summary>
    /// Repository interface cho ServiceInvoice.
    /// </summary>
    public interface IServiceInvoiceRepository
    {
        /// <summary>
        /// Lấy danh sách hóa đơn dịch vụ có phân trang.
        /// </summary>
        Task<(List<ServiceInvoice> Items, int TotalCount)> GetPagedListAsync(
            string? keyword,
            byte? paymentStatus,
            DateTime? fromDate,
            DateTime? toDate,
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending);

        /// <summary>
        /// Lấy hóa đơn theo Id, bao gồm Ticket, Quotation, Items, không tracking.
        /// </summary>
        Task<ServiceInvoice?> GetByIdWithDetailsAsync(int id);

        /// <summary>
        /// Lấy hóa đơn của một phiếu sửa chữa (1:1), không tracking.
        /// </summary>
        Task<ServiceInvoice?> GetByTicketIdAsync(int ticketId);

        /// <summary>
        /// Lấy mã hóa đơn cuối cùng của một ngày (để sinh mã tự động SRV-yyyyMMdd-NNN).
        /// </summary>
        Task<string?> GetLastInvoiceCodeByDateAsync(string datePrefix);

        /// <summary>
        /// Lấy hóa đơn theo Id, với tracking để update.
        /// </summary>
        Task<ServiceInvoice?> GetByIdWithTrackingAsync(int id);

        /// <summary>
        /// Kiểm tra phiếu đã có hóa đơn chưa.
        /// </summary>
        Task<bool> InvoiceExistsForTicketAsync(int ticketId);

        Task AddAsync(ServiceInvoice invoice);
        Task SaveChangesAsync();
    }
}
