using System.Net.Http.Json;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Inventory;

namespace Client.Services.Inventory
{
    public class ProductSerialClientService : IProductSerialClientService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "api/product-serials";

        public ProductSerialClientService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ApiResult<bool>> CheckExistAsync(string serialNumber, int variantId)
        {
            try
            {
                var url = $"{BaseUrl}/check-exist?serialNumber={Uri.EscapeDataString(serialNumber)}&variantId={variantId}";
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return ApiResult<bool>.Fail($"Lỗi HTTP {(int)response.StatusCode}.");
                var result = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();
                return result ?? ApiResult<bool>.Fail("Không thể kiểm tra mã Serial.");
            }
            catch (Exception ex)
            {
                return ApiResult<bool>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<PagedResult<ProductSerialListDto>>> GetPagedListAsync(ProductSerialFilterRequest filter)
        {
            try
            {
                var queryParams = new List<string>();
                if (!string.IsNullOrWhiteSpace(filter.Keyword))
                    queryParams.Add($"keyword={Uri.EscapeDataString(filter.Keyword)}");
                if (filter.ProductId.HasValue)
                    queryParams.Add($"productId={filter.ProductId}");
                if (filter.VariantId.HasValue)
                    queryParams.Add($"variantId={filter.VariantId}");
                if (filter.Status.HasValue)
                    queryParams.Add($"status={filter.Status}");
                if (filter.FromDate.HasValue)
                    queryParams.Add($"fromDate={filter.FromDate:yyyy-MM-dd}");
                if (filter.ToDate.HasValue)
                    queryParams.Add($"toDate={filter.ToDate:yyyy-MM-dd}");
                queryParams.Add($"pageNumber={filter.PageNumber}");
                queryParams.Add($"pageSize={filter.PageSize}");
                if (!string.IsNullOrWhiteSpace(filter.SortBy))
                    queryParams.Add($"sortBy={Uri.EscapeDataString(filter.SortBy)}");
                queryParams.Add($"sortDescending={filter.SortDescending}");

                var url = $"{BaseUrl}?{string.Join("&", queryParams)}";
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return ApiResult<PagedResult<ProductSerialListDto>>.Fail($"Lỗi HTTP {(int)response.StatusCode}.");
                var result = await response.Content.ReadFromJsonAsync<ApiResult<PagedResult<ProductSerialListDto>>>();
                return result ?? ApiResult<PagedResult<ProductSerialListDto>>.Fail("Không thể lấy danh sách Serial.");
            }
            catch (Exception ex)
            {
                return ApiResult<PagedResult<ProductSerialListDto>>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<ProductSerialStatisticsDto>> GetStatisticsAsync(int? productId = null, int? variantId = null)
        {
            try
            {
                var queryParams = new List<string>();
                if (productId.HasValue)
                    queryParams.Add($"productId={productId}");
                if (variantId.HasValue)
                    queryParams.Add($"variantId={variantId}");

                var url = queryParams.Count > 0
                    ? $"{BaseUrl}/statistics?{string.Join("&", queryParams)}"
                    : $"{BaseUrl}/statistics";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return ApiResult<ProductSerialStatisticsDto>.Fail($"Lỗi HTTP {(int)response.StatusCode}.");
                var result = await response.Content.ReadFromJsonAsync<ApiResult<ProductSerialStatisticsDto>>();
                return result ?? ApiResult<ProductSerialStatisticsDto>.Fail("Không thể lấy thống kê Serial.");
            }
            catch (Exception ex)
            {
                return ApiResult<ProductSerialStatisticsDto>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<ProductSerialDetailDto>> GetByIdAsync(int id)
        {
            try
            {
                var url = $"{BaseUrl}/{id}";
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return ApiResult<ProductSerialDetailDto>.Fail($"Lỗi HTTP {(int)response.StatusCode}.");
                var result = await response.Content.ReadFromJsonAsync<ApiResult<ProductSerialDetailDto>>();
                return result ?? ApiResult<ProductSerialDetailDto>.Fail("Không thể lấy chi tiết Serial.");
            }
            catch (Exception ex)
            {
                return ApiResult<ProductSerialDetailDto>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<bool>> UpdateStatusAsync(int id, UpdateSerialStatusRequest request)
        {
            try
            {
                var url = $"{BaseUrl}/{id}/status";
                var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Patch, url)
                {
                    Content = JsonContent.Create(request)
                });
                if (!response.IsSuccessStatusCode)
                    return ApiResult<bool>.Fail($"Lỗi HTTP {(int)response.StatusCode}.");
                var result = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();
                return result ?? ApiResult<bool>.Fail("Không thể cập nhật trạng thái Serial.");
            }
            catch (Exception ex)
            {
                return ApiResult<bool>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }
    }
}
