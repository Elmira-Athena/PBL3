using PBL3.Core.Entities;

namespace PBL3.Core.Interfaces
{
    /// <summary>
    /// Repository interface cho ProductSerial.
    /// </summary>
    public interface IProductSerialRepository
    {
        /// <summary>
        /// Kiểm tra danh sách Serial đã tồn tại trong DB chưa.
        /// Trả về danh sách Serial bị trùng.
        /// </summary>
        Task<List<string>> GetExistingSerialsAsync(List<string> serialNumbers);

        /// <summary>
        /// Kiểm tra một Serial Number đã tồn tại trong DB chưa (theo VariantId).
        /// Dùng cho check real-time khi quét mã vạch.
        /// </summary>
        Task<bool> ExistsAsync(string serialNumber, int variantId);

        /// <summary>
        /// Lấy danh sách SerialNumber theo ReceiptId và VariantId.
        /// </summary>
        Task<List<string>> GetSerialsByReceiptAndVariantAsync(int receiptId, int variantId);

        Task AddRangeAsync(IEnumerable<ProductSerial> serials);

        /// <summary>
        /// Lấy chi tiết Serial kèm Variant và Product.
        /// </summary>
        Task<ProductSerial?> GetBySerialNumberAsync(string serialNumber);

        /// <summary>
        /// Lấy danh sách ProductSerial theo SerialNumbers (WITH TRACKING để update).
        /// Dùng cho luồng xuất kho — cần ghi nhận trạng thái Sold.
        /// </summary>
        Task<List<ProductSerial>> GetSerialsWithTrackingAsync(List<string> serialNumbers);

        /// <summary>
        /// Lấy chi tiết Serial theo Id (WITH TRACKING để update).
        /// </summary>
        Task<ProductSerial?> GetByIdWithTrackingAsync(int id);

        /// <summary>
        /// Lấy N Serials đang Available của một Variant (dùng cho hàng generic khi checkout).
        /// </summary>
        Task<List<ProductSerial>> GetAvailableSerialsByVariantAsync(int variantId, int count);

        /// <summary>
        /// Đếm số Serial Available (Status=0) theo danh sách VariantId — batch query.
        /// </summary>
        Task<Dictionary<int, int>> CountAvailableByVariantIdsAsync(List<int> variantIds);

        /// <summary>
        /// Lấy tất cả serials Available theo danh sách VariantId (batch) — dùng cho snapshot kiểm kê.
        /// Trả về (SerialId, VariantId, SerialNumber).
        /// </summary>
        Task<List<(int SerialId, int VariantId, string SerialNumber)>> GetAvailableSerialsBatchAsync(List<int> variantIds);

        Task SaveChangesAsync();

        /// <summary>
        /// Danh sách phân trang ProductSerial với bộ lọc đa điều kiện.
        /// </summary>
        Task<(List<ProductSerial> Items, int TotalCount)> GetPagedListAsync(
            string? keyword, int? productId, int? variantId,
            byte? status, DateTime? fromDate, DateTime? toDate,
            int pageNumber, int pageSize, string? sortBy, bool sortDescending);

        /// <summary>
        /// Đếm số Serial theo trạng thái (GROUP BY Status). Có thể lọc theo productId hoặc variantId.
        /// </summary>
        Task<Dictionary<byte, int>> GetStatusCountsAsync(int? productId, int? variantId);

        /// <summary>
        /// Lấy chi tiết Serial kèm Variant, Product, ImportReceipt, Supplier (read-only).
        /// </summary>
        Task<ProductSerial?> GetByIdWithDetailsAsync(int id);
    }
}
