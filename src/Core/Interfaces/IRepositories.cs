using PBL3.Core.Entities;

namespace PBL3.Core.Interfaces
{
    /// <summary>
    /// Repository interface cho Category.
    /// </summary>
    public interface ICategoryRepository
    {
        Task<List<Category>> GetAllActiveAsync();
        Task<Category?> GetByIdAsync(int id, bool includeParent = false);
        Task<bool> IsDuplicateNameAsync(int? parentId, string name, int? excludeId = null);
        Task<bool> IsDuplicateSlugAsync(string slug, int? excludeId = null);
        Task<bool> HasActiveChildrenAsync(int id);
        Task<bool> HasProductsAsync(int id);
        Task<Dictionary<int, int?>> GetAllCategoryParentMapAsync();
        Task<List<Category>> GetChildrenAsync(int parentId);
        Task AddAsync(Category category);
        Task SaveChangesAsync();
    }

    /// <summary>
    /// Repository interface cho Product.
    /// </summary>
    public interface IProductRepository
    {
        Task<Product?> GetByIdAsync(int id);
        Task<Product?> GetByIdWithDetailsAsync(int id);
        Task<bool> IsSkuExistsAsync(string sku, int? excludeVariantId = null);
        Task<bool> ManufacturerExistsAsync(int manufacturerId);
        Task<bool> CategoryExistsAsync(int categoryId);

        /// <summary>
        /// Lấy danh sách Id category con (đệ quy) từ một category cha.
        /// </summary>
        Task<List<int>> GetCategoryChildIdsAsync(int categoryId);

        /// <summary>
        /// Lấy danh sách sản phẩm có phân trang và bộ lọc.
        /// Trả về (items, totalCount).
        /// </summary>
        Task<(List<Product> Items, int TotalCount)> GetPagedListAsync(
            string? keyword,
            List<int>? categoryIds,
            int? manufacturerId,
            decimal? priceMin,
            decimal? priceMax,
            int? status,
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending);

        Task AddAsync(Product product);
        Task AddVariantAsync(ProductVariant variant);
        Task RemoveVariant(ProductVariant variant);
        Task SaveChangesAsync();
    }

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
