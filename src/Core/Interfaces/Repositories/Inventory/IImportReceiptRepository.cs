using PBL3.Core.Entities;

namespace PBL3.Core.Interfaces
{
    /// <summary>
    /// Repository interface cho ImportReceipt.
    /// </summary>
    public interface IImportReceiptRepository
    {
        /// <summary>
        /// Lấy danh sách phiếu nhập có phân trang, tìm kiếm và Include Supplier.
        /// </summary>
        Task<(List<ImportReceipt> Items, int TotalCount)> GetPagedListAsync(
            string? keyword,
            DateTime? fromDate,
            DateTime? toDate,
            int? supplierId,
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending);

        /// <summary>
        /// Lấy chi tiết phiếu nhập theo Id, bao gồm Details, Variant, và ProductSerials.
        /// </summary>
        Task<ImportReceipt?> GetByIdWithDetailsAsync(int id);

        /// <summary>
        /// Lấy mã phiếu nhập cuối cùng theo ngày (để sinh mã tự động).
        /// </summary>
        Task<string?> GetLastReceiptCodeByDateAsync(string datePrefix);

        Task AddAsync(ImportReceipt receipt);
        Task AddDetailAsync(ImportReceiptDetail detail);
        Task SaveChangesAsync();
    }
}
