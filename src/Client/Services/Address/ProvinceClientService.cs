using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using Client.Services.Common;
using PBL3.Shared.DTOs.Common;

namespace Client.Services.Address
{
    /// <summary>
    /// Tra cứu tỉnh/thành và phường/xã qua Provinces API (bên ngoài, không cần auth).
    ///
    /// Không dùng ApiCall.SendAsync: API này trả JSON trần, không bọc ApiResult&lt;T&gt;,
    /// nên cũng không có câu tiếng Việt nào của server để giữ lại. Nhưng vẫn dùng
    /// GetAsync rồi tự kiểm status, và vẫn log — trước đây hai trang gọi
    /// thẳng API này và nuốt lỗi bằng catch trống.
    /// </summary>
    public class ProvinceClientService : IProvinceClientService
    {
        public const string HttpClientName = "ProvincesAPI";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ProvinceClientService> _logger;

        public ProvinceClientService(IHttpClientFactory httpClientFactory, ILogger<ProvinceClientService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public Task<ApiResult<List<ProvinceItem>>> GetProvincesAsync() =>
            GetAsync<List<ProvinceItem>>("api/v2/p/", "tải danh sách tỉnh/thành phố",
                "Không tải được danh sách tỉnh/thành phố. Vui lòng thử lại.");

        public async Task<ApiResult<List<WardItem>>> GetWardsAsync(int provinceCode)
        {
            var result = await GetAsync<ProvinceDetail>(
                $"api/v2/p/{provinceCode}?depth=2", "tải danh sách phường/xã",
                "Không tải được danh sách phường/xã. Vui lòng thử lại.");

            return result.Success
                ? ApiResult<List<WardItem>>.Ok(result.Data?.Wards ?? new List<WardItem>())
                : ApiResult<List<WardItem>>.Fail(result.Message);
        }

        private async Task<ApiResult<T>> GetAsync<T>(string url, string action, string failMessage)
        {
            var http = _httpClientFactory.CreateClient(HttpClientName);
            try
            {
                var response = await http.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("HTTP {StatusCode} khi {Action}.", (int)response.StatusCode, action);
                    return ApiResult<T>.Fail(ApiCall.ToUserMessage(response.StatusCode, action));
                }

                var body = await response.Content.ReadFromJsonAsync<T>();
                if (body is not null) return ApiResult<T>.Ok(body);

                _logger.LogError("Thân phản hồi rỗng khi {Action}.", action);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", action);
            }

            return ApiResult<T>.Fail(failMessage);
        }
    }
}
