using PBL3.Core.Entities;

namespace PBL3.Core.Interfaces
{
    /// <summary>
    /// Repository interface cho Supplier.
    /// </summary>
    public interface ISupplierRepository
    {
        /// <summary>
        /// Lấy danh sách nhà cung cấp có phân trang và tìm kiếm.
        /// </summary>
        Task<(List<Supplier> Items, int TotalCount)> GetPagedListAsync(
            string? keyword,
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending);

        /// <summary>
        /// Lấy chi tiết nhà cung cấp theo Id.
        /// </summary>
        Task<Supplier?> GetByIdAsync(int id);

        /// <summary>
        /// Kiểm tra nhà cung cấp có phiếu nhập kho nào không (dùng cho logic xoá).
        /// </summary>
        Task<bool> HasImportReceiptsAsync(int supplierId);

        Task AddAsync(Supplier supplier);
        Task SaveChangesAsync();
    }
}
