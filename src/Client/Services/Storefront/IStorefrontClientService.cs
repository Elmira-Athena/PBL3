using PBL3.Shared.DTOs.Storefront;
using PBL3.Shared.DTOs.Common;

namespace Client.Services.Storefront
{
    public interface IStorefrontClientService
    {
        Task<ApiResult<List<CategoryMenuResponse>>> GetActiveCategoriesAsync();
        Task<ApiResult<List<ProductCardResponse>>> GetFeaturedProductsAsync(int? categoryId, int take = 10);
    }
}
