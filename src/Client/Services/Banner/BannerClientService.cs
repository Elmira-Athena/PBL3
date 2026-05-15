using System.Net.Http.Json;
using PBL3.Shared.DTOs.Banners;
using PBL3.Shared.DTOs.Common;

namespace Client.Services.Banner
{
    public class BannerClientService : IBannerClientService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "api/banners";

        public BannerClientService(HttpClient httpClient)
        {
            _httpClient = httpClient;
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
                return ApiResult<PagedResult<BannerDto>>.Fail($"Lỗi kết nối: {ex.Message}");
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
                return ApiResult<BannerDto>.Fail($"Lỗi kết nối: {ex.Message}");
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
                return ApiResult<BannerDto>.Fail($"Lỗi kết nối: {ex.Message}");
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
                return ApiResult<BannerDto>.Fail($"Lỗi kết nối: {ex.Message}");
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
                return ApiResult<bool>.Fail($"Lỗi kết nối: {ex.Message}");
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
                return ApiResult<List<BannerPublicDto>>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }
    }
}
