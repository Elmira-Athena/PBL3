using PBL3.Shared.DTOs.Analytics;
using PBL3.Shared.DTOs.Common;

namespace PBL3.Application.Analytics
{
    public interface IAnalyticsService
    {
        Task<ApiResult<AnalyticsSummaryDto>> GetSummaryAsync(DateTime from, DateTime to);
        Task<ApiResult<RevenueTrendDto>> GetRevenueTrendAsync(DateTime from, DateTime to);
        Task<ApiResult<OrderChannelDto>> GetOrderChannelsAsync(DateTime from, DateTime to);
        Task<ApiResult<List<TopProductDto>>> GetTopProductsAsync(DateTime from, DateTime to, int top);
        Task<ApiResult<List<CategoryRevenueDto>>> GetCategoryRevenueAsync(DateTime from, DateTime to, int top);
        Task<ApiResult<InventorySummaryDto>> GetInventorySummaryAsync();
    }
}
