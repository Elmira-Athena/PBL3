using Client.Services.Common;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using PBL3.Shared.DTOs.Analytics;
using PBL3.Shared.DTOs.Common;

namespace Client.Services.Analytics
{
    public class AnalyticsClientService : IAnalyticsClientService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AnalyticsClientService> _logger;
        private const string BaseUrl = "api/analytics";

        public AnalyticsClientService(HttpClient httpClient, ILogger<AnalyticsClientService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        private string DateRange(DateTime from, DateTime to) =>
            $"from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";

        public async Task<ApiResult<AnalyticsSummaryDto>> GetSummaryAsync(DateTime from, DateTime to)
        {
            return await ApiCall.SendAsync<AnalyticsSummaryDto>(
                () => _httpClient.GetAsync($"{BaseUrl}/summary?{DateRange(from, to)}"),
                "tải số liệu tổng quan", _logger);
        }

        public async Task<ApiResult<RevenueTrendDto>> GetRevenueTrendAsync(DateTime from, DateTime to)
        {
            return await ApiCall.SendAsync<RevenueTrendDto>(
                () => _httpClient.GetAsync($"{BaseUrl}/revenue-trend?{DateRange(from, to)}"),
                "tải biểu đồ doanh thu", _logger);
        }

        public async Task<ApiResult<OrderChannelDto>> GetOrderChannelsAsync(DateTime from, DateTime to)
        {
            return await ApiCall.SendAsync<OrderChannelDto>(
                () => _httpClient.GetAsync($"{BaseUrl}/order-channels?{DateRange(from, to)}"),
                "tải tỉ lệ kênh bán", _logger);
        }

        public async Task<ApiResult<List<TopProductDto>>> GetTopProductsAsync(DateTime from, DateTime to, int top = 10)
        {
            return await ApiCall.SendAsync<List<TopProductDto>>(
                () => _httpClient.GetAsync($"{BaseUrl}/top-products?{DateRange(from, to)}&top={top}"),
                "tải danh sách sản phẩm bán chạy", _logger);
        }

        public async Task<ApiResult<List<CategoryRevenueDto>>> GetCategoryRevenueAsync(DateTime from, DateTime to, int top = 5)
        {
            return await ApiCall.SendAsync<List<CategoryRevenueDto>>(
                () => _httpClient.GetAsync($"{BaseUrl}/category-revenue?{DateRange(from, to)}&top={top}"),
                "tải doanh thu theo danh mục", _logger);
        }

        public async Task<ApiResult<InventorySummaryDto>> GetInventorySummaryAsync()
        {
            return await ApiCall.SendAsync<InventorySummaryDto>(
                () => _httpClient.GetAsync($"{BaseUrl}/inventory-summary"),
                "tải số liệu tồn kho", _logger);
        }
    }
}
