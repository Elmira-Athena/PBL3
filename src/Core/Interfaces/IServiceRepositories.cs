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
        Task<List<string>> GetCodesByDatePrefixAsync(string datePrefix);

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
        /// CHỈ ĐỌC — entity trả về KHÔNG nằm trong Change Tracker, mọi lệnh gán lên nó
        /// sẽ bị SaveChangesAsync bỏ qua im lặng. Cần ghi thì dùng
        /// GetByIdWithTrackingAsync hoặc MarkPendingAsSupersededAsync.
        /// </summary>
        Task<List<Quotation>> GetByTicketIdReadOnlyAsync(int ticketId);

        /// <summary>
        /// Đánh dấu MỌI báo giá đang "Chờ duyệt" (0) của phiếu thành "Bị thay thế" (3)
        /// bằng một câu UPDATE set-based duy nhất.
        ///
        /// Vì sao là set-based chứ không phải đọc-rồi-gán: cách cũ đọc qua
        /// GetByTicketIdAsync (AsNoTracking) rồi gán q.Status = 3, nên KHÔNG sinh ra
        /// câu UPDATE nào — bất biến "chỉ tồn tại duy nhất một báo giá có hiệu lực"
        /// chưa bao giờ tồn tại. Một câu UPDATE ... WHERE Status = 0 vừa sửa lỗi đó,
        /// vừa đóng luôn khe check-then-act: hai request tạo báo giá song song không
        /// thể cùng để lại hai bản Status = 0.
        ///
        /// LƯU Ý: ExecuteUpdateAsync thực thi NGAY, không đợi SaveChangesAsync — phải
        /// gọi bên trong transaction đang mở. Nó cũng bỏ qua Change Tracker.
        /// </summary>
        /// <returns>Số báo giá bị đánh dấu.</returns>
        Task<int> MarkPendingAsSupersededAsync(int ticketId);

        /// <summary>
        /// CỔNG NGUYÊN TỬ cho quyết định của khách trên một báo giá: chuyển
        /// <paramref name="fromStatus"/> → <paramref name="toStatus"/> bằng một câu
        /// UPDATE ... WHERE Status = fromStatus.
        ///
        /// Vì sao cần: cả hai nhánh duyệt và từ chối đều theo mẫu check-then-act —
        /// đọc quotation.Status, kiểm, rồi mới ghi ở một câu lệnh khác. Hai request
        /// đồng thời cùng qua được câu kiểm trước khi ai kịp ghi, nên một báo giá
        /// được xử lý HAI lần và phiếu rơi vào trạng thái mâu thuẫn với lịch sử.
        /// Mở transaction bao quanh KHÔNG sửa được: dưới READ COMMITTED, shared lock
        /// của câu SELECT nhả ngay khi đọc xong.
        ///
        /// Câu UPDATE này thì nguyên tử: DB lấy row lock rồi ĐÁNH GIÁ LẠI vị từ trên
        /// bản mới nhất, nên đúng một request thắng.
        /// </summary>
        /// <returns>true nếu request này thắng; false nếu người khác đã xử lý trước.</returns>
        Task<bool> TryDecideAsync(int quotationId, byte fromStatus, byte toStatus, DateTime decidedAt, string? note);

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
        /// Lấy phiếu RMA của một phiếu sửa chữa (1:1). CHỈ ĐỌC — không tracking,
        /// mọi lệnh gán lên entity trả về sẽ bị SaveChangesAsync bỏ qua im lặng.
        /// </summary>
        Task<RmaShipment?> GetByTicketIdReadOnlyAsync(int ticketId);

        /// <summary>
        /// Như trên nhưng CÓ tracking — dùng cho đường ghi (ghi nhận kết quả từ hãng).
        /// </summary>
        Task<RmaShipment?> GetByTicketIdTrackedAsync(int ticketId);

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
        /// <summary>
        /// CHỈ ĐỌC — không tracking. Cùng bẫy như các repository khác: gán lên entity
        /// trả về sẽ không bao giờ vào DB.
        /// </summary>
        Task<ServiceInvoice?> GetByTicketIdReadOnlyAsync(int ticketId);

        /// <summary>
        /// Lấy mã hóa đơn cuối cùng của một ngày (để sinh mã tự động SRV-yyyyMMdd-NNN).
        /// </summary>
        Task<List<string>> GetCodesByDatePrefixAsync(string datePrefix);

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
