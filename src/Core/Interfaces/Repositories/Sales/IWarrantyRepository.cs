using PBL3.Core.Entities;

namespace PBL3.Core.Interfaces
{
    /// <summary>
    /// Repository interface cho Warranty.
    /// </summary>
    public interface IWarrantyRepository
    {
        /// <summary>
        /// Lấy danh sách bảo hành active (status != Claimed) của một serial, sắp xếp theo EndDate giảm dần.
        /// </summary>
        Task<List<Warranty>> GetActiveBySerialIdAsync(int serialId);

        /// <summary>
        /// Lấy bảo hành theo Id, có tracking để update.
        /// </summary>
        Task<Warranty?> GetByIdWithTrackingAsync(int id);

        Task AddAsync(Warranty warranty);
        Task AddRangeAsync(IEnumerable<Warranty> warranties);
        Task SaveChangesAsync();
    }
}
