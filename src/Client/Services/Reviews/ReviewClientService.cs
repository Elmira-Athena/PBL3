using System.Net.Http.Json;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Reviews;

namespace Client.Services.Reviews
{
    public class ReviewClientService : IReviewClientService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "api/reviews";

        public ReviewClientService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ApiResult<PagedResult<ReviewDto>>?> GetReviewsAsync(
            int productId, int page, int pageSize)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<ApiResult<PagedResult<ReviewDto>>>(
                    $"{BaseUrl}?productId={productId}&page={page}&pageSize={pageSize}");
            }
            catch
            {
                return ApiResult<PagedResult<ReviewDto>>.Fail("Không thể tải danh sách đánh giá.");
            }
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
