using Microsoft.Extensions.Logging;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Storefront;
using System.Net.Http.Json;

namespace Client.Services.Storefront
{
    public class StorefrontClientService(
        HttpClient httpClient,
        ILogger<StorefrontClientService> logger) : IStorefrontClientService
    {
        private readonly HttpClient _httpClient = httpClient;
        private readonly ILogger<StorefrontClientService> _logger = logger;

        public async Task<ApiResult<List<CategoryMenuResponse>>> GetActiveCategoriesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/storefront/categories");
                if (!response.IsSuccessStatusCode)
                    return ApiResult<List<CategoryMenuResponse>>.Fail($"Lỗi HTTP {(int)response.StatusCode}.");
                var result = await response.Content.ReadFromJsonAsync<ApiResult<List<CategoryMenuResponse>>>();
                return result ?? ApiResult<List<CategoryMenuResponse>>.Fail("Lỗi kết nối server.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "tải menu danh mục");
                return ApiResult<List<CategoryMenuResponse>>.Fail("Không tải được menu danh mục. Vui lòng tải lại trang.");
            }
        }

        public async Task<ApiResult<List<ProductCardResponse>>> GetFeaturedProductsAsync(int? categoryId, int take = 10)
        {
            try
            {
                var url = $"/api/storefront/products/featured?take={take}";
                if (categoryId.HasValue)
                {
                    url += $"&categoryId={categoryId.Value}";
                }

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return ApiResult<List<ProductCardResponse>>.Fail($"Lỗi HTTP {(int)response.StatusCode}.");
                var result = await response.Content.ReadFromJsonAsync<ApiResult<List<ProductCardResponse>>>();
                return result ?? ApiResult<List<ProductCardResponse>>.Fail("Lỗi kết nối server.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "tải sản phẩm nổi bật");
                return ApiResult<List<ProductCardResponse>>.Fail("Không tải được sản phẩm nổi bật. Vui lòng tải lại trang.");
            }
        }

        public async Task<ApiResult<ProductDetailResponse>> GetProductDetailAsync(string slug)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/storefront/products/{slug}");
                if (!response.IsSuccessStatusCode)
                    return ApiResult<ProductDetailResponse>.Fail($"Lỗi HTTP {(int)response.StatusCode}.");
                var result = await response.Content.ReadFromJsonAsync<ApiResult<ProductDetailResponse>>();
                return result ?? ApiResult<ProductDetailResponse>.Fail("Lỗi kết nối server.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "tải trang chi tiết sản phẩm");
                return ApiResult<ProductDetailResponse>.Fail("Không tải được thông tin sản phẩm. Vui lòng tải lại trang.");
            }
        }

        public async Task<ApiResult<List<ProductCardResponse>>> GetRelatedProductsAsync(string slug, int take = 5)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/storefront/products/{slug}/related?take={take}");
                if (!response.IsSuccessStatusCode)
                    return ApiResult<List<ProductCardResponse>>.Fail($"Lỗi HTTP {(int)response.StatusCode}.");
                var result = await response.Content.ReadFromJsonAsync<ApiResult<List<ProductCardResponse>>>();
                return result ?? ApiResult<List<ProductCardResponse>>.Fail("Lỗi kết nối server.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "tải sản phẩm liên quan");
                return ApiResult<List<ProductCardResponse>>.Fail("Không tải được sản phẩm liên quan. Vui lòng tải lại trang.");
            }
        }

        public async Task<ApiResult<CategoryDetailResponse>> GetCategoryBySlugAsync(string slug)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/storefront/categories/{slug}");
                if (!response.IsSuccessStatusCode)
                    return ApiResult<CategoryDetailResponse>.Fail($"Lỗi HTTP {(int)response.StatusCode}.");
                var result = await response.Content.ReadFromJsonAsync<ApiResult<CategoryDetailResponse>>();
                return result ?? ApiResult<CategoryDetailResponse>.Fail("Lỗi kết nối server.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "tải thông tin danh mục");
                return ApiResult<CategoryDetailResponse>.Fail("Không tải được thông tin danh mục. Vui lòng tải lại trang.");
            }
        }

        public async Task<ApiResult<PagedResult<ProductCardResponse>>> GetProductsByCategoryAsync(
            string slug, int page = 1, int pageSize = 20)
        {
            try
            {
                var response = await _httpClient.GetAsync(
                    $"/api/storefront/categories/{slug}/products?page={page}&pageSize={pageSize}");
                if (!response.IsSuccessStatusCode)
                    return ApiResult<PagedResult<ProductCardResponse>>.Fail($"Lỗi HTTP {(int)response.StatusCode}.");
                var result = await response.Content.ReadFromJsonAsync<ApiResult<PagedResult<ProductCardResponse>>>();
                return result ?? ApiResult<PagedResult<ProductCardResponse>>.Fail("Lỗi kết nối server.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "tải sản phẩm theo danh mục");
                return ApiResult<PagedResult<ProductCardResponse>>.Fail("Không tải được sản phẩm của danh mục. Vui lòng thử lại.");
            }
        }

        public async Task<ApiResult<PagedResult<ProductCardResponse>>> SearchProductsAsync(
            string? keyword, int? categoryId, decimal? priceMin, decimal? priceMax,
            int page = 1, int pageSize = 20)
        {
            try
            {
                var queryParams = new List<string>();

                if (!string.IsNullOrWhiteSpace(keyword))
                    queryParams.Add($"keyword={Uri.EscapeDataString(keyword)}");
                if (categoryId.HasValue)
                    queryParams.Add($"categoryId={categoryId.Value}");
                if (priceMin.HasValue)
                    queryParams.Add($"priceMin={priceMin.Value}");
                if (priceMax.HasValue)
                    queryParams.Add($"priceMax={priceMax.Value}");

                queryParams.Add($"page={page}");
                queryParams.Add($"pageSize={pageSize}");

                var url = $"/api/storefront/products/search?{string.Join("&", queryParams)}";
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return ApiResult<PagedResult<ProductCardResponse>>.Fail($"Lỗi HTTP {(int)response.StatusCode}.");
                var result = await response.Content.ReadFromJsonAsync<ApiResult<PagedResult<ProductCardResponse>>>();
                return result ?? ApiResult<PagedResult<ProductCardResponse>>.Fail("Lỗi kết nối server.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "tìm kiếm sản phẩm");
                return ApiResult<PagedResult<ProductCardResponse>>.Fail("Không tìm kiếm được sản phẩm. Vui lòng thử lại.");
            }
        }
    }
}
