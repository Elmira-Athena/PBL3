using System.Net.Http.Json;
using PBL3.Shared.DTOs.Analytics;
using PBL3.Shared.DTOs.Common;

namespace Client.Services.Analytics
{
    public class AnalyticsClientService : IAnalyticsClientService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "api/analytics";

        public AnalyticsClientService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        private string DateRange(DateTime from, DateTime to) =>
            $"from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";

        public async Task<ApiResult<AnalyticsSummaryDto>> GetSummaryAsync(DateTime from, DateTime to)
        {
            try
            {
                var result = await _httpClient
                    .GetFromJsonAsync<ApiResult<AnalyticsSummaryDto>>($"{BaseUrl}/summary?{DateRange(from, to)}");
                return result ?? ApiResult<AnalyticsSummaryDto>.Fail("Không nhận được phản hồi từ máy chủ.");
            }
            catch (Exception ex)
            {
                return ApiResult<AnalyticsSummaryDto>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<RevenueTrendDto>> GetRevenueTrendAsync(DateTime from, DateTime to)
        {
            try
            {
                var result = await _httpClient
                    .GetFromJsonAsync<ApiResult<RevenueTrendDto>>($"{BaseUrl}/revenue-trend?{DateRange(from, to)}");
                return result ?? ApiResult<RevenueTrendDto>.Fail("Không nhận được phản hồi từ máy chủ.");
            }
            catch (Exception ex)
            {
                return ApiResult<RevenueTrendDto>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<OrderChannelDto>> GetOrderChannelsAsync(DateTime from, DateTime to)
        {
            try
            {
                var result = await _httpClient
                    .GetFromJsonAsync<ApiResult<OrderChannelDto>>($"{BaseUrl}/order-channels?{DateRange(from, to)}");
                return result ?? ApiResult<OrderChannelDto>.Fail("Không nhận được phản hồi từ máy chủ.");
            }
            catch (Exception ex)
            {
                return ApiResult<OrderChannelDto>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<List<TopProductDto>>> GetTopProductsAsync(DateTime from, DateTime to, int top = 10)
        {
            try
            {
                var result = await _httpClient
                    .GetFromJsonAsync<ApiResult<List<TopProductDto>>>($"{BaseUrl}/top-products?{DateRange(from, to)}&top={top}");
                return result ?? ApiResult<List<TopProductDto>>.Fail("Không nhận được phản hồi từ máy chủ.");
            }
            catch (Exception ex)
            {
                return ApiResult<List<TopProductDto>>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<List<CategoryRevenueDto>>> GetCategoryRevenueAsync(DateTime from, DateTime to, int top = 5)
        {
            try
            {
                var result = await _httpClient
                    .GetFromJsonAsync<ApiResult<List<CategoryRevenueDto>>>($"{BaseUrl}/category-revenue?{DateRange(from, to)}&top={top}");
                return result ?? ApiResult<List<CategoryRevenueDto>>.Fail("Không nhận được phản hồi từ máy chủ.");
            }
            catch (Exception ex)
            {
                return ApiResult<List<CategoryRevenueDto>>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<InventorySummaryDto>> GetInventorySummaryAsync()
        {
            try
            {
                var result = await _httpClient
                    .GetFromJsonAsync<ApiResult<InventorySummaryDto>>($"{BaseUrl}/inventory-summary");
                return result ?? ApiResult<InventorySummaryDto>.Fail("Không nhận được phản hồi từ máy chủ.");
            }
            catch (Exception ex)
            {
                return ApiResult<InventorySummaryDto>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }
    }
}
