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

    /// <summary>
    /// Repository interface cho Quotation.
    /// </summary>
    public interface IQuotationRepository
    {
        /// <summary>
        /// Lấy báo giá theo Id, bao gồm Items, không tracking.
        /// </summary>
        Task<Quotation?> GetByIdWithItemsAsync(int id);

        /// <summary>
        /// Lấy báo giá theo Id, với tracking để update.
        /// </summary>
        Task<Quotation?> GetByIdWithTrackingAsync(int id);

        /// <summary>
        /// Lấy danh sách báo giá của một phiếu (allow multiple revisions).
        /// </summary>
        Task<List<Quotation>> GetByTicketIdAsync(int ticketId);

        /// <summary>
        /// Kiểm tra phiếu đã có báo giá được chấp nhận (status=Accepted) hay không.
        /// </summary>
        Task<bool> HasAcceptedQuotationAsync(int ticketId);

        Task AddAsync(Quotation quotation);
        Task SaveChangesAsync();
    }

    /// <summary>
    /// Repository interface cho RmaShipment.
    /// </summary>
    public interface IRmaShipmentRepository
    {
        /// <summary>
        /// Lấy phiếu RMA của một phiếu sửa chữa (1:1), không tracking.
        /// </summary>
        Task<RmaShipment?> GetByTicketIdAsync(int ticketId);

        /// <summary>
        /// Lấy RMA theo Id, với tracking để update.
        /// </summary>
        Task<RmaShipment?> GetByIdWithTrackingAsync(int id);

        Task AddAsync(RmaShipment shipment);
        Task SaveChangesAsync();
    }

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

    /// <summary>
    /// Repository interface cho SerialRepairLog.
    /// </summary>
    public interface ISerialRepairLogRepository
    {
        /// <summary>
        /// Lấy danh sách lịch sửa chữa của một serial (history lâu dài).
        /// </summary>
        Task<List<SerialRepairLog>> GetBySerialIdAsync(int serialId);

        /// <summary>
        /// Lấy log theo Id, với tracking để update.
        /// </summary>
        Task<SerialRepairLog?> GetByIdWithTrackingAsync(int id);

        Task AddAsync(SerialRepairLog log);
        Task SaveChangesAsync();
    }
}
