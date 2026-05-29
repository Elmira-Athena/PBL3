using PBL3.Core.Entities;

namespace PBL3.Core.Interfaces
{
    /// <summary>
    /// Repository interface cho Manufacturer (Hãng sản xuất).
    /// </summary>
    public interface IManufacturerRepository
    {
        /// <summary>
        /// Lấy danh sách hãng sản xuất có phân trang và tìm kiếm.
        /// </summary>
        Task<(List<Manufacturer> Items, int TotalCount)> GetPagedListAsync(
            string? keyword,
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending);

        /// <summary>
        /// Lấy chi tiết hãng sản xuất theo Id.
        /// </summary>
        Task<Manufacturer?> GetByIdAsync(int id);

        /// <summary>
        /// Lấy danh sách tất cả hãng (Id + Name) dùng cho dropdown.
        /// </summary>
        Task<List<Manufacturer>> GetAllActiveAsync();

        /// <summary>
        /// Kiểm tra hãng có sản phẩm nào đang active không (dùng cho logic xoá).
        /// </summary>
        Task<bool> HasProductsAsync(int manufacturerId);

        /// <summary>
        /// Kiểm tra tên hãng đã tồn tại chưa (tránh trùng lặp).
        /// </summary>
        Task<bool> IsDuplicateNameAsync(string name, int? excludeId = null);

        Task AddAsync(Manufacturer manufacturer);
        Task SaveChangesAsync();
    }
}
