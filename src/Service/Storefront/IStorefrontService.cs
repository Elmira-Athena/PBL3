using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Storefront;

namespace PBL3.Service.Storefront
{
    public interface IStorefrontService
    {
        Task<ApiResult<List<CategoryMenuResponse>>> GetActiveCategoriesAsync();
        Task<ApiResult<List<ProductCardResponse>>> GetFeaturedProductsAsync(int? categoryId, int take = 5);
        Task<ApiResult<ProductDetailResponse>> GetProductDetailAsync(string slug);
        Task<ApiResult<List<ProductCardResponse>>> GetRelatedProductsAsync(string slug);
    }
}
