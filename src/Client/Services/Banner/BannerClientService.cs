using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using PBL3.Shared.DTOs.Banners;
using PBL3.Shared.DTOs.Common;

namespace Client.Services.Banner
{
    public class BannerClientService : IBannerClientService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<BannerClientService> _logger;
        private const string BaseUrl = "api/banners";

        public BannerClientService(HttpClient httpClient, ILogger<BannerClientService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<ApiResult<PagedResult<BannerDto>>> GetListAsync(BannerFilterRequest request)
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

                var result = await _httpClient.GetFromJsonAsync<ApiResult<PagedResult<BannerDto>>>(url);
                return result ?? ApiResult<PagedResult<BannerDto>>.Fail("Không thể tải danh sách banner.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "tải danh sách banner");
                return ApiResult<PagedResult<BannerDto>>.Fail("Không tải được danh sách banner. Vui lòng thử lại.");
            }
        }

        public async Task<ApiResult<BannerDto>> GetByIdAsync(int id)
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<ApiResult<BannerDto>>($"{BaseUrl}/{id}");
                return result ?? ApiResult<BannerDto>.Fail("Không tìm thấy banner.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "tải thông tin banner");
                return ApiResult<BannerDto>.Fail("Không tải được thông tin banner. Vui lòng thử lại.");
            }
        }

        public async Task<ApiResult<BannerDto>> CreateAsync(CreateBannerRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(BaseUrl, request);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<BannerDto>>();
                return result ?? ApiResult<BannerDto>.Fail("Không thể tạo banner.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "tạo banner");
                return ApiResult<BannerDto>.Fail("Không tạo được banner. Vui lòng thử lại.");
            }
        }

        public async Task<ApiResult<BannerDto>> UpdateAsync(int id, UpdateBannerRequest request)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"{BaseUrl}/{id}", request);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<BannerDto>>();
                return result ?? ApiResult<BannerDto>.Fail("Không thể cập nhật banner.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "cập nhật banner");
                return ApiResult<BannerDto>.Fail("Không cập nhật được banner. Vui lòng thử lại.");
            }
        }

        public async Task<ApiResult<bool>> DeleteAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{BaseUrl}/{id}");
                var result = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();
                return result ?? ApiResult<bool>.Fail("Không thể xóa banner.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "xoá banner");
                return ApiResult<bool>.Fail("Không xoá được banner. Vui lòng thử lại.");
            }
        }

        public async Task<ApiResult<List<BannerPublicDto>>> GetActiveAsync()
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<ApiResult<List<BannerPublicDto>>>($"{BaseUrl}/active");
                return result ?? ApiResult<List<BannerPublicDto>>.Fail("Không thể tải banner trang chủ.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "tải banner đang hiển thị");
                return ApiResult<List<BannerPublicDto>>.Fail("Không tải được banner trang chủ. Vui lòng tải lại trang.");
            }
        }
    }
}
