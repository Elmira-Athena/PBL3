using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Products;

namespace Client.Services.Product
{
    public interface IProductClientService
    {
        // ----- Product CRUD -----
        Task<ApiResult<PagedResult<ProductListDto>>> GetListAsync(ProductFilterRequest request);
        Task<ApiResult<ProductDetailDto>> GetByIdAsync(int id);
        Task<ApiResult<ProductDetailDto>> CreateAsync(CreateProductRequest request);
        Task<ApiResult<ProductDetailDto>> UpdateAsync(int id, UpdateProductRequest request);
        Task<ApiResult<bool>> DeleteAsync(int id);

        // ----- Variant CRUD (variant-scoped) -----
        Task<ApiResult<ProductVariantDto>> GetVariantAsync(int variantId);
        Task<ApiResult<ProductVariantDto>> CreateVariantAsync(int productId, SaveVariantRequest request);
        Task<ApiResult<ProductVariantDto>> UpdateVariantAsync(int variantId, UpdateVariantRequest request);
        Task<ApiResult<bool>> UpdateVariantImagesAsync(int variantId, List<SaveImageRequest> images);
        Task<ApiResult<bool>> UpdateVariantSpecificationsAsync(int variantId, Dictionary<string, string> specifications);
        Task<ApiResult<bool>> DeleteVariantAsync(int variantId);
    }
}
