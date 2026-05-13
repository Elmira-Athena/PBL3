using PBL3.Shared.DTOs.Analytics;
using PBL3.Shared.DTOs.Common;

namespace Client.Services.Analytics
{
    public interface IAnalyticsClientService
    {
        Task<ApiResult<AnalyticsSummaryDto>> GetSummaryAsync(DateTime from, DateTime to);
        Task<ApiResult<RevenueTrendDto>> GetRevenueTrendAsync(DateTime from, DateTime to);
        Task<ApiResult<OrderChannelDto>> GetOrderChannelsAsync(DateTime from, DateTime to);
        Task<ApiResult<List<TopProductDto>>> GetTopProductsAsync(DateTime from, DateTime to, int top = 10);
        Task<ApiResult<List<CategoryRevenueDto>>> GetCategoryRevenueAsync(DateTime from, DateTime to, int top = 5);
        Task<ApiResult<InventorySummaryDto>> GetInventorySummaryAsync();
    }
}
