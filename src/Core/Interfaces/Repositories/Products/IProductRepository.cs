using PBL3.Core.Entities;

namespace PBL3.Core.Interfaces
{
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

        /// <summary>
        /// Lấy danh sách VariantId tồn tại (chưa bị xoá) từ danh sách Id.
        /// </summary>
        Task<List<int>> GetExistingVariantIdsAsync(List<int> variantIds);

        /// <summary>
        /// Lấy danh sách CategoryId (distinct) của các sản phẩm chứa Variant trong danh sách.
        /// Dùng để kiểm tra điều kiện danh mục áp dụng của voucher trong checkout.
        /// </summary>
        Task<List<int>> GetCategoryIdsByVariantIdsAsync(List<int> variantIds);

        /// <summary>
        /// Lọc ProductVariant theo thông số kỹ thuật JSON.
        /// </summary>
        Task<List<ProductVariant>> FilterBySpecificationAsync(string specKey, string specValue);

        /// <summary>
        /// Lấy Variant theo Id (có tracking để update).
        /// </summary>
        Task<ProductVariant?> GetVariantByIdAsync(int variantId);

        Task AddAsync(Product product);
        Task AddVariantAsync(ProductVariant variant);
        Task RemoveVariant(ProductVariant variant);
        Task ReplaceProductImagesAsync(int productId, List<ProductImage> newImages);
        Task SaveChangesAsync();
    }
}
