using PBL3.Core.Entities;

namespace PBL3.Core.Interfaces
{
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
}
