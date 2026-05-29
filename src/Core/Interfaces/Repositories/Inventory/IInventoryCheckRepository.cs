using PBL3.Core.Entities;

namespace PBL3.Core.Interfaces
{
    /// <summary>
    /// Repository interface cho InventoryCheck (Kiểm kê kho hàng).
    /// </summary>
    public interface IInventoryCheckRepository
    {
        /// <summary>
        /// Lấy phiếu kiểm kê theo Id (WithTracking cho update).
        /// </summary>
        Task<InventoryCheck?> GetByIdAsync(int id);

        /// <summary>
        /// Lấy phiếu kiểm kê theo Id kèm Details + DetailSerials (AsNoTracking, read-only).
        /// </summary>
        Task<InventoryCheck?> GetByIdWithDetailsAsync(int id);

        /// <summary>
        /// Lấy danh sách phiếu kiểm kê phân trang.
        /// </summary>
        Task<(List<InventoryCheck> Items, int TotalCount)> GetPagedListAsync(
            string? keyword,
            byte? status,
            DateTime? fromDate,
            DateTime? toDate,
            Guid? employeeId,
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending);

        /// <summary>
        /// Lấy mã phiếu kiểm kê cuối cùng theo ngày (để sinh mã tự động KK-yyyyMMdd-NNN).
        /// </summary>
        Task<string?> GetLastCheckCodeByDateAsync(string datePrefix);

        /// <summary>
        /// Lấy 1 row InventoryCheckDetailSerial theo Id.
        /// </summary>
        Task<InventoryCheckDetailSerial?> GetDetailSerialAsync(int detailSerialId, bool withTracking = false);

        /// <summary>
        /// Lấy row InventoryCheckDetailSerial theo CheckId + SerialId (WithTracking để update khi quét).
        /// </summary>
        Task<InventoryCheckDetailSerial?> GetPendingDetailSerialBySerialIdAsync(int checkId, int serialId);

        /// <summary>
        /// Kiểm tra SerialNumberRaw đã được quét trong phiếu này chưa (chống quét trùng).
        /// </summary>
        Task<bool> IsSerialAlreadyScannedAsync(int checkId, string serialNumberRaw);

        /// <summary>
        /// Lấy tất cả rows Pending của phiếu (để chuyển Missing khi submit).
        /// </summary>
        Task<List<InventoryCheckDetailSerial>> GetPendingDetailSerialsAsync(int checkId);

        /// <summary>
        /// Lấy tất cả rows Surplus/UnknownSurplus của phiếu (để xóa khi reject returnToDraft).
        /// </summary>
        Task<List<InventoryCheckDetailSerial>> GetSurplusDetailSerialsAsync(int checkId);

        /// <summary>
        /// Lấy danh sách InventoryCheckDetailSerial phân trang, lọc theo ScanStatus.
        /// </summary>
        Task<(List<InventoryCheckDetailSerial> Items, int TotalCount)> GetDetailSerialsPagedAsync(
            int checkId,
            byte? scanStatus,
            int? variantId,
            int pageNumber,
            int pageSize);

        /// <summary>
        /// Đếm số lượng theo từng ScanStatus của phiếu (phục vụ dashboard).
        /// </summary>
        Task<Dictionary<byte, int>> GetGroupedCountsByCheckAsync(int checkId);

        /// <summary>
        /// Lấy InventoryCheckDetail (WithTracking) để cập nhật counts sau khi quét.
        /// </summary>
        Task<InventoryCheckDetail?> GetDetailByCheckAndVariantAsync(int checkId, int variantId, bool withTracking = false);

        /// <summary>
        /// Lấy tất cả rows Missing kèm ProductSerial hiện tại (để approve - mark Lost).
        /// Trả về kèm tracking để có thể cập nhật ProductSerial.Status.
        /// </summary>
        Task<List<InventoryCheckDetailSerial>> GetMissingDetailSerialsWithSerialAsync(int checkId);

        /// <summary>
        /// Lấy tất cả rows Defective kèm ProductSerial (để approve - mark Defective).
        /// </summary>
        Task<List<InventoryCheckDetailSerial>> GetDefectiveDetailSerialsWithSerialAsync(int checkId);

        Task AddAsync(InventoryCheck check);
        Task AddDetailAsync(InventoryCheckDetail detail);
        Task AddDetailSerialAsync(InventoryCheckDetailSerial detailSerial);
        Task AddDetailSerialsAsync(IEnumerable<InventoryCheckDetailSerial> detailSerials);
        Task AddAdjustmentLogsAsync(IEnumerable<InventoryAdjustmentLog> logs);
        Task RemoveDetailSerialsAsync(IEnumerable<InventoryCheckDetailSerial> serials);
        Task SaveChangesAsync();
    }
}
