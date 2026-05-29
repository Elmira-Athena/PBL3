using PBL3.Core.Entities;

namespace PBL3.Core.Interfaces
{
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
