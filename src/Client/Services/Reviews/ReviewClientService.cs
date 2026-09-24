using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using Client.Services.Common;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Reviews;

namespace Client.Services.Reviews
{
    public class ReviewClientService : IReviewClientService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ReviewClientService> _logger;
        private const string BaseUrl = "api/reviews";

        public ReviewClientService(HttpClient httpClient, ILogger<ReviewClientService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<ApiResult<PagedResult<ReviewDto>>?> GetReviewsAsync(
            int productId, int page, int pageSize)
        {
            return await ApiCall.SendAsync<PagedResult<ReviewDto>>(
                () => _httpClient.GetAsync($"{BaseUrl}?productId={productId}&page={page}&pageSize={pageSize}"),
                "tải danh sách đánh giá", _logger);
        }

        public async Task<ApiResult<ReviewDto>?> CreateAsync(CreateReviewRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync(BaseUrl, request);
            try
            {
                return await response.Content.ReadFromJsonAsync<ApiResult<ReviewDto>>();
            }
            catch
            {
                var body = await response.Content.ReadAsStringAsync();
                var hint = body.Length > 0 ? body[..Math.Min(body.Length, 300)] : $"HTTP {(int)response.StatusCode}";
                return ApiResult<ReviewDto>.Fail(hint);
            }
        }

        public async Task<ApiResult<bool>?> DeleteAsync(int reviewId)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{BaseUrl}/{reviewId}");
                return await response.Content.ReadFromJsonAsync<ApiResult<bool>>();
            }
            catch
            {
                return ApiResult<bool>.Fail("Không thể xóa đánh giá.");
            }
        }
    }
}
