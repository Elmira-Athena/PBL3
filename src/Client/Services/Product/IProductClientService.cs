using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Products;

namespace Client.Services.Product
{
    public interface IProductClientService
    {
        Task<ApiResult<PagedResult<ProductListDto>>> GetListAsync(ProductFilterRequest request);
        Task<ApiResult<ProductDetailDto>> GetByIdAsync(int id);
        Task<ApiResult<ProductDetailDto>> CreateAsync(CreateProductRequest request);
        Task<ApiResult<ProductDetailDto>> UpdateAsync(int id, UpdateProductRequest request);
        Task<ApiResult<ProductVariantDto>> AddVariantAsync(int productId, SaveVariantRequest request);
        Task<ApiResult<bool>> DeleteAsync(int id);
    }
}
