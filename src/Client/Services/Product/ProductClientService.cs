using System.Net.Http.Json;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Products;

namespace Client.Services.Product
{
    public class ProductClientService : IProductClientService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "api/products";

        public ProductClientService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ApiResult<PagedResult<ProductListDto>>> GetListAsync(ProductFilterRequest request)
        {
            try
            {
                var queryParams = new List<string>();

                if (!string.IsNullOrWhiteSpace(request.Keyword))
                    queryParams.Add($"Keyword={Uri.EscapeDataString(request.Keyword)}");
                if (request.CategoryId.HasValue)
                    queryParams.Add($"CategoryId={request.CategoryId}");
                if (request.ManufacturerId.HasValue)
                    queryParams.Add($"ManufacturerId={request.ManufacturerId}");
                if (request.PriceMin.HasValue)
                    queryParams.Add($"PriceMin={request.PriceMin}");
                if (request.PriceMax.HasValue)
                    queryParams.Add($"PriceMax={request.PriceMax}");
                if (request.Status.HasValue)
                    queryParams.Add($"Status={(int)request.Status}");

                queryParams.Add($"PageNumber={request.PageNumber}");
                queryParams.Add($"PageSize={request.PageSize}");

                if (!string.IsNullOrWhiteSpace(request.SortBy))
                    queryParams.Add($"SortBy={Uri.EscapeDataString(request.SortBy)}");
                if (request.SortDescending)
                    queryParams.Add("SortDescending=true");

                var url = $"{BaseUrl}?{string.Join("&", queryParams)}";

                var result = await _httpClient
                    .GetFromJsonAsync<ApiResult<PagedResult<ProductListDto>>>(url);
                return result ?? ApiResult<PagedResult<ProductListDto>>.Fail("Không thể tải danh sách sản phẩm.");
            }
            catch (Exception ex)
            {
                return ApiResult<PagedResult<ProductListDto>>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<ProductDetailDto>> GetByIdAsync(int id)
        {
            try
            {
                var result = await _httpClient
                    .GetFromJsonAsync<ApiResult<ProductDetailDto>>($"{BaseUrl}/{id}");
                return result ?? ApiResult<ProductDetailDto>.Fail("Không tìm thấy sản phẩm.");
            }
            catch (Exception ex)
            {
                return ApiResult<ProductDetailDto>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<ProductDetailDto>> CreateAsync(CreateProductRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(BaseUrl, request);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<ProductDetailDto>>();
                return result ?? ApiResult<ProductDetailDto>.Fail("Không thể tạo sản phẩm.");
            }
            catch (Exception ex)
            {
                return ApiResult<ProductDetailDto>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<ProductDetailDto>> UpdateAsync(int id, UpdateProductRequest request)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"{BaseUrl}/{id}", request);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<ProductDetailDto>>();
                return result ?? ApiResult<ProductDetailDto>.Fail("Không thể cập nhật sản phẩm.");
            }
            catch (Exception ex)
            {
                return ApiResult<ProductDetailDto>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<ProductVariantDto>> AddVariantAsync(int productId, SaveVariantRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/{productId}/variants", request);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<ProductVariantDto>>();
                return result ?? ApiResult<ProductVariantDto>.Fail("Không thể lưu biến thể.");
            }
            catch (Exception ex)
            {
                return ApiResult<ProductVariantDto>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<bool>> DeleteAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{BaseUrl}/{id}");
                var result = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();
                return result ?? ApiResult<bool>.Fail("Không thể xóa sản phẩm.");
            }
            catch (Exception ex)
            {
                return ApiResult<bool>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }
    }
}
