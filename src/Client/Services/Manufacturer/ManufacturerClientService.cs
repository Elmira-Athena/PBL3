using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Manufacturers;

namespace Client.Services.Manufacturer
{
    public class ManufacturerClientService : IManufacturerClientService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ManufacturerClientService> _logger;
        private const string BaseUrl = "api/manufacturers";

        public ManufacturerClientService(HttpClient httpClient, ILogger<ManufacturerClientService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<ApiResult<PagedResult<ManufacturerDto>>> GetListAsync(ManufacturerFilterRequest request)
        {
            try
            {
                var queryParams = new List<string>();

                if (!string.IsNullOrWhiteSpace(request.Keyword))
                    queryParams.Add($"Keyword={Uri.EscapeDataString(request.Keyword)}");

                queryParams.Add($"PageNumber={request.PageNumber}");
                queryParams.Add($"PageSize={request.PageSize}");

                if (!string.IsNullOrWhiteSpace(request.SortBy))
                    queryParams.Add($"SortBy={Uri.EscapeDataString(request.SortBy)}");
                if (request.SortDescending)
                    queryParams.Add("SortDescending=true");

                var url = $"{BaseUrl}?{string.Join("&", queryParams)}";

                var result = await _httpClient
                    .GetFromJsonAsync<ApiResult<PagedResult<ManufacturerDto>>>(url);
                return result ?? ApiResult<PagedResult<ManufacturerDto>>.Fail("Không thể tải danh sách hãng sản xuất.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "tải danh sách hãng sản xuất");
                return ApiResult<PagedResult<ManufacturerDto>>.Fail("Không tải được danh sách hãng sản xuất. Vui lòng thử lại.");
            }
        }

        public async Task<ApiResult<List<ManufacturerSummaryDto>>> GetDropdownAsync()
        {
            try
            {
                var result = await _httpClient
                    .GetFromJsonAsync<ApiResult<List<ManufacturerSummaryDto>>>($"{BaseUrl}/dropdown");
                return result ?? ApiResult<List<ManufacturerSummaryDto>>.Fail("Không thể tải danh sách hãng.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "tải hãng sản xuất cho ô chọn");
                return ApiResult<List<ManufacturerSummaryDto>>.Fail("Không tải được danh sách hãng sản xuất. Vui lòng thử lại.");
            }
        }

        public async Task<ApiResult<ManufacturerDto>> GetByIdAsync(int id)
        {
            try
            {
                var result = await _httpClient
                    .GetFromJsonAsync<ApiResult<ManufacturerDto>>($"{BaseUrl}/{id}");
                return result ?? ApiResult<ManufacturerDto>.Fail("Không tìm thấy hãng sản xuất.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "tải thông tin hãng sản xuất");
                return ApiResult<ManufacturerDto>.Fail("Không tải được thông tin hãng sản xuất. Vui lòng thử lại.");
            }
        }

        public async Task<ApiResult<ManufacturerDto>> CreateAsync(CreateManufacturerRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(BaseUrl, request);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<ManufacturerDto>>();
                return result ?? ApiResult<ManufacturerDto>.Fail("Không thể tạo hãng sản xuất.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "tạo hãng sản xuất");
                return ApiResult<ManufacturerDto>.Fail("Không tạo được hãng sản xuất. Vui lòng thử lại.");
            }
        }

        public async Task<ApiResult<ManufacturerDto>> UpdateAsync(int id, UpdateManufacturerRequest request)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"{BaseUrl}/{id}", request);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<ManufacturerDto>>();
                return result ?? ApiResult<ManufacturerDto>.Fail("Không thể cập nhật hãng sản xuất.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "cập nhật hãng sản xuất");
                return ApiResult<ManufacturerDto>.Fail("Không cập nhật được hãng sản xuất. Vui lòng thử lại.");
            }
        }

        public async Task<ApiResult<bool>> DeleteAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{BaseUrl}/{id}");
                var result = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();
                return result ?? ApiResult<bool>.Fail("Không thể xóa hãng sản xuất.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "xoá hãng sản xuất");
                return ApiResult<bool>.Fail("Không xoá được hãng sản xuất. Vui lòng thử lại.");
            }
        }
    }
}
