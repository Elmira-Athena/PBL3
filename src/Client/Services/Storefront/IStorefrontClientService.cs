using PBL3.Shared.DTOs.Storefront;
using PBL3.Shared.DTOs.Common;

namespace Client.Services.Storefront
{
    public interface IStorefrontClientService
    {
        Task<ApiResult<List<CategoryMenuResponse>>> GetActiveCategoriesAsync();
        Task<ApiResult<List<ProductCardResponse>>> GetFeaturedProductsAsync(int? categoryId, int take = 10);
        Task<ApiResult<ProductDetailResponse>> GetProductDetailAsync(string slug);
        Task<ApiResult<List<ProductCardResponse>>> GetRelatedProductsAsync(string slug, int take = 5);

        /// <summary>Lấy thông tin danh mục theo slug.</summary>
        Task<ApiResult<CategoryDetailResponse>> GetCategoryBySlugAsync(string slug);

        /// <summary>Lấy danh sách sản phẩm theo danh mục (cả danh mục con) với phân trang.</summary>
        Task<ApiResult<PagedResult<ProductCardResponse>>> GetProductsByCategoryAsync(string slug, int page = 1, int pageSize = 20);

        /// <summary>Tìm kiếm sản phẩm theo từ khóa, danh mục và khoảng giá với phân trang.</summary>
        Task<ApiResult<PagedResult<ProductCardResponse>>> SearchProductsAsync(
            string? keyword, int? categoryId, decimal? priceMin, decimal? priceMax,
            int page = 1, int pageSize = 20);
    }
}
