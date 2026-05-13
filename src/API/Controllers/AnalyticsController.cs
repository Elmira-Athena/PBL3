using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBL3.Service.Analytics;
using PBL3.Shared.DTOs.Analytics;

namespace PBL3.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize(Roles = "Admin")]
    public class AnalyticsController : ControllerBase
    {
        private readonly IAnalyticsService _service;

        public AnalyticsController(IAnalyticsService service)
        {
            _service = service;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary([FromQuery] AnalyticsFilterRequest filter)
        {
            var result = await _service.GetSummaryAsync(filter.From, filter.To);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("revenue-trend")]
        public async Task<IActionResult> GetRevenueTrend([FromQuery] AnalyticsFilterRequest filter)
        {
            var result = await _service.GetRevenueTrendAsync(filter.From, filter.To);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("order-channels")]
        public async Task<IActionResult> GetOrderChannels([FromQuery] AnalyticsFilterRequest filter)
        {
            var result = await _service.GetOrderChannelsAsync(filter.From, filter.To);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("top-products")]
        public async Task<IActionResult> GetTopProducts([FromQuery] AnalyticsFilterRequest filter, [FromQuery] int top = 10)
        {
            var result = await _service.GetTopProductsAsync(filter.From, filter.To, top);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("category-revenue")]
        public async Task<IActionResult> GetCategoryRevenue([FromQuery] AnalyticsFilterRequest filter, [FromQuery] int top = 5)
        {
            var result = await _service.GetCategoryRevenueAsync(filter.From, filter.To, top);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("inventory-summary")]
        public async Task<IActionResult> GetInventorySummary()
        {
            var result = await _service.GetInventorySummaryAsync();
            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
}
