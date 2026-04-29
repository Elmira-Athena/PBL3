using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Storefront;
using System.Net.Http.Json;

namespace Client.Services.Storefront
{
    public class StorefrontClientService(HttpClient httpClient) : IStorefrontClientService
    {
        private readonly HttpClient _httpClient = httpClient;

        public async Task<ApiResult<List<CategoryMenuResponse>>> GetActiveCategoriesAsync()
        {
            var response = await _httpClient.GetAsync("/api/storefront/categories");
            var result = await response.Content.ReadFromJsonAsync<ApiResult<List<CategoryMenuResponse>>>();
            return result ?? ApiResult<List<CategoryMenuResponse>>.Fail("Lỗi kết nối server.");
        }

        public async Task<ApiResult<List<ProductCardResponse>>> GetFeaturedProductsAsync(int? categoryId, int take = 10)
        {
            var url = $"/api/storefront/products/featured?take={take}";
            if (categoryId.HasValue)
            {
                url += $"&categoryId={categoryId.Value}";
            }
            
            var response = await _httpClient.GetAsync(url);
            var result = await response.Content.ReadFromJsonAsync<ApiResult<List<ProductCardResponse>>>();
            return result ?? ApiResult<List<ProductCardResponse>>.Fail("Lỗi kết nối server.");
        }

        public async Task<ApiResult<ProductDetailResponse>> GetProductDetailAsync(string slug)
        {
            var response = await _httpClient.GetAsync($"/api/storefront/products/{slug}");
            var result = await response.Content.ReadFromJsonAsync<ApiResult<ProductDetailResponse>>();
            return result ?? ApiResult<ProductDetailResponse>.Fail("Lỗi kết nối server.");
        }

        public async Task<ApiResult<List<ProductCardResponse>>> GetRelatedProductsAsync(string slug, int take = 5)
        {
            var response = await _httpClient.GetAsync($"/api/storefront/products/{slug}/related?take={take}");
            var result = await response.Content.ReadFromJsonAsync<ApiResult<List<ProductCardResponse>>>();
            return result ?? ApiResult<List<ProductCardResponse>>.Fail("Lỗi kết nối server.");
        }
    }
}
