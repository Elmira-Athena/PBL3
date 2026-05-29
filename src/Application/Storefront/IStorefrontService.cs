using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Storefront;

namespace PBL3.Application.Storefront
{
    public interface IStorefrontService
    {
        Task<ApiResult<List<CategoryMenuResponse>>> GetActiveCategoriesAsync();
        Task<ApiResult<List<ProductCardResponse>>> GetFeaturedProductsAsync(int? categoryId, int take = 5);
        Task<ApiResult<ProductDetailResponse>> GetProductDetailAsync(string slug);
        Task<ApiResult<List<ProductCardResponse>>> GetRelatedProductsAsync(string slug);

        /// <summary>
        /// Lấy thông tin danh mục theo slug (dùng cho breadcrumb và tiêu đề trang).
        /// </summary>
        Task<ApiResult<CategoryDetailResponse>> GetCategoryBySlugAsync(string slug);

        /// <summary>
        /// Lấy danh sách sản phẩm theo danh mục (bao gồm cả danh mục con) với phân trang.
        /// </summary>
        Task<ApiResult<PagedResult<ProductCardResponse>>> GetProductsByCategoryAsync(string categorySlug, int page, int pageSize);

        /// <summary>
        /// Tìm kiếm sản phẩm theo từ khóa, danh mục và khoảng giá với phân trang.
        /// </summary>
        Task<ApiResult<PagedResult<ProductCardResponse>>> SearchProductsAsync(
            string? keyword, int? categoryId, decimal? priceMin, decimal? priceMax,
            int page, int pageSize);
    }
}
