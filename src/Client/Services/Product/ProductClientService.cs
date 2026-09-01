using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Products;

namespace Client.Services.Product
{
    public class ProductClientService : IProductClientService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ProductClientService> _logger;
        private const string BaseUrl = "api/products";

        public ProductClientService(HttpClient httpClient, ILogger<ProductClientService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
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
                _logger.LogError(ex, "Lỗi khi {Action}.", "tải danh sách sản phẩm");
                return ApiResult<PagedResult<ProductListDto>>.Fail("Không tải được danh sách sản phẩm. Vui lòng thử lại.");
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
                _logger.LogError(ex, "Lỗi khi {Action}.", "tải chi tiết sản phẩm");
                return ApiResult<ProductDetailDto>.Fail("Không tải được chi tiết sản phẩm. Vui lòng thử lại.");
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
                _logger.LogError(ex, "Lỗi khi {Action}.", "tạo sản phẩm");
                return ApiResult<ProductDetailDto>.Fail("Không tạo được sản phẩm. Vui lòng thử lại.");
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
                _logger.LogError(ex, "Lỗi khi {Action}.", "cập nhật sản phẩm");
                return ApiResult<ProductDetailDto>.Fail("Không cập nhật được sản phẩm. Vui lòng thử lại.");
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
                _logger.LogError(ex, "Lỗi khi {Action}.", "xoá sản phẩm");
                return ApiResult<bool>.Fail("Không xoá được sản phẩm. Vui lòng thử lại.");
            }
        }

        // ============= VARIANT ENDPOINTS =============

        public async Task<ApiResult<ProductVariantDto>> GetVariantAsync(int variantId)
        {
            try
            {
                var result = await _httpClient
                    .GetFromJsonAsync<ApiResult<ProductVariantDto>>($"{BaseUrl}/variants/{variantId}");
                return result ?? ApiResult<ProductVariantDto>.Fail("Không tìm thấy phiên bản.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "tải thông tin phiên bản sản phẩm");
                return ApiResult<ProductVariantDto>.Fail("Không tải được thông tin phiên bản sản phẩm. Vui lòng thử lại.");
            }
        }

        public async Task<ApiResult<ProductVariantDto>> CreateVariantAsync(int productId, SaveVariantRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/{productId}/variants", request);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<ProductVariantDto>>();
                return result ?? ApiResult<ProductVariantDto>.Fail("Không thể tạo phiên bản.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "tạo phiên bản sản phẩm");
                return ApiResult<ProductVariantDto>.Fail("Không tạo được phiên bản sản phẩm. Vui lòng thử lại.");
            }
        }

        public async Task<ApiResult<ProductVariantDto>> UpdateVariantAsync(int variantId, UpdateVariantRequest request)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"{BaseUrl}/variants/{variantId}", request);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<ProductVariantDto>>();
                return result ?? ApiResult<ProductVariantDto>.Fail("Không thể cập nhật phiên bản.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "cập nhật phiên bản sản phẩm");
                return ApiResult<ProductVariantDto>.Fail("Không cập nhật được phiên bản sản phẩm. Vui lòng thử lại.");
            }
        }

        public async Task<ApiResult<bool>> UpdateVariantImagesAsync(int variantId, List<SaveImageRequest> images)
        {
            try
            {
                var body = new SaveVariantImagesRequest { Images = images };
                var response = await _httpClient.PutAsJsonAsync($"{BaseUrl}/variants/{variantId}/images", body);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();
                return result ?? ApiResult<bool>.Fail("Không thể cập nhật ảnh phiên bản.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "cập nhật ảnh phiên bản sản phẩm");
                return ApiResult<bool>.Fail("Không cập nhật được ảnh sản phẩm. Vui lòng thử lại.");
            }
        }

        public async Task<ApiResult<bool>> UpdateVariantSpecificationsAsync(int variantId, Dictionary<string, string> specifications)
        {
            try
            {
                var body = new SaveVariantSpecificationsRequest { Specifications = specifications };
                var response = await _httpClient.PutAsJsonAsync($"{BaseUrl}/variants/{variantId}/specifications", body);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();
                return result ?? ApiResult<bool>.Fail("Không thể cập nhật thông số kỹ thuật.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "cập nhật thông số phiên bản sản phẩm");
                return ApiResult<bool>.Fail("Không cập nhật được thông số sản phẩm. Vui lòng thử lại.");
            }
        }

        public async Task<ApiResult<bool>> DeleteVariantAsync(int variantId)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{BaseUrl}/variants/{variantId}");
                var result = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();
                return result ?? ApiResult<bool>.Fail("Không thể xoá phiên bản.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "xoá phiên bản sản phẩm");
                return ApiResult<bool>.Fail("Không xoá được phiên bản sản phẩm. Vui lòng thử lại.");
            }
        }
    }
}
