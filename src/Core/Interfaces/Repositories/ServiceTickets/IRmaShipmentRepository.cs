using PBL3.Core.Entities;

namespace PBL3.Core.Interfaces
{
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
}
