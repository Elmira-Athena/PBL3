using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PBL3.Infrastructure.Data;
using PBL3.Shared.DTOs.Analytics;
using PBL3.Shared.DTOs.Common;

namespace PBL3.Service.Analytics
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly HushStoreDbContext _context;
        private readonly ILogger<AnalyticsService> _logger;

        public AnalyticsService(HushStoreDbContext context, ILogger<AnalyticsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        private (bool valid, string error) ValidateRange(DateTime from, DateTime to)
        {
            if (to < from)
                return (false, "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.");
            if ((to - from).TotalDays > 366)
                return (false, "Khoảng thời gian thống kê tối đa là 366 ngày.");
            return (true, string.Empty);
        }

        public async Task<ApiResult<AnalyticsSummaryDto>> GetSummaryAsync(DateTime from, DateTime to)
        {
            var (valid, error) = ValidateRange(from, to);
            if (!valid) return ApiResult<AnalyticsSummaryDto>.Fail(error);
            try
            {
                var fromDate = from.Date;
                var toDate = to.Date.AddDays(1);

                var successQ = _context.Orders.AsNoTracking()
                    .Where(o => o.Status == 3 && o.OrderDate >= fromDate && o.OrderDate < toDate);

                var totalRevenue = await successQ.SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;
                var totalOrders = await successQ.CountAsync();

                var totalCost = await (
                    from o in _context.Orders
                    where o.Status == 3 && o.OrderDate >= fromDate && o.OrderDate < toDate
                    join od in _context.OrderDetails on o.Id equals od.OrderId
                    join os in _context.OrderSerials on od.Id equals os.OrderDetailId
                    join ps in _context.ProductSerials on os.SerialId equals ps.Id
                    join ird in _context.ImportReceiptDetails
                        on new { ps.ImportReceiptId, ps.VariantId }
                        equals new { ImportReceiptId = ird.ReceiptId, ird.VariantId }
                    select (decimal?)ird.ImportPrice
                ).SumAsync() ?? 0m;

                var cancelledOrders = await _context.Orders.AsNoTracking()
                    .Where(o => o.Status == 4 && o.OrderDate >= fromDate && o.OrderDate < toDate)
                    .CountAsync();

                var allOrdersInWindow = await _context.Orders.AsNoTracking()
                    .Where(o => o.OrderDate >= fromDate && o.OrderDate < toDate)
                    .CountAsync();

                var newCustomers = await _context.Users.AsNoTracking()
                    .Where(u => u.Type == 2 && !u.IsDeleted
                             && u.CreatedDate >= fromDate && u.CreatedDate < toDate)
                    .CountAsync();

                var paymentCounts = await _context.Orders.AsNoTracking()
                    .Where(o => o.Status == 3 && o.OrderDate >= fromDate && o.OrderDate < toDate)
                    .GroupBy(o => o.PaymentMethod)
                    .Select(g => new { Method = g.Key, Count = g.Count() })
                    .ToListAsync();

                var cancelRate = allOrdersInWindow > 0
                    ? Math.Round((decimal)cancelledOrders / allOrdersInWindow * 100, 1)
                    : 0m;

                var dto = new AnalyticsSummaryDto
                {
                    TotalRevenue = totalRevenue,
                    GrossProfit = totalRevenue - totalCost,
                    TotalOrders = totalOrders,
                    CancelledOrders = cancelledOrders,
                    CancelRate = cancelRate,
                    AverageOrderValue = totalOrders > 0 ? Math.Round(totalRevenue / totalOrders, 0) : 0m,
                    NewCustomers = newCustomers,
                    PayCod = paymentCounts.FirstOrDefault(x => x.Method == 0)?.Count ?? 0,
                    PayBanking = paymentCounts.FirstOrDefault(x => x.Method == 1)?.Count ?? 0,
                    PayVnPay = paymentCounts.FirstOrDefault(x => x.Method == 2)?.Count ?? 0
                };
                return ApiResult<AnalyticsSummaryDto>.Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy tổng quan thống kê.");
                return ApiResult<AnalyticsSummaryDto>.Fail("Không thể tải dữ liệu thống kê tổng quan.");
            }
        }

        public async Task<ApiResult<RevenueTrendDto>> GetRevenueTrendAsync(DateTime from, DateTime to)
        {
            var (valid, error) = ValidateRange(from, to);
            if (!valid) return ApiResult<RevenueTrendDto>.Fail(error);
            try
            {
                var fromDate = from.Date;
                var toDate = to.Date.AddDays(1);

                var revenueByDay = await _context.Orders.AsNoTracking()
                    .Where(o => o.Status == 3 && o.OrderDate >= fromDate && o.OrderDate < toDate)
                    .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month, o.OrderDate.Day })
                    .Select(g => new { g.Key, Revenue = g.Sum(o => o.TotalAmount) })
                    .ToDictionaryAsync(
                        x => new DateTime(x.Key.Year, x.Key.Month, x.Key.Day),
                        x => x.Revenue);

                var costByDay = await (
                    from o in _context.Orders
                    where o.Status == 3 && o.OrderDate >= fromDate && o.OrderDate < toDate
                    join od in _context.OrderDetails on o.Id equals od.OrderId
                    join os in _context.OrderSerials on od.Id equals os.OrderDetailId
                    join ps in _context.ProductSerials on os.SerialId equals ps.Id
                    join ird in _context.ImportReceiptDetails
                        on new { ps.ImportReceiptId, ps.VariantId }
                        equals new { ImportReceiptId = ird.ReceiptId, ird.VariantId }
                    group ird.ImportPrice by new { o.OrderDate.Year, o.OrderDate.Month, o.OrderDate.Day } into g
                    select new { g.Key, Cost = g.Sum() }
                ).ToDictionaryAsync(
                    x => new DateTime(x.Key.Year, x.Key.Month, x.Key.Day),
                    x => x.Cost);

                var points = new List<DailyRevenuePointDto>();
                for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
                {
                    var rev = revenueByDay.GetValueOrDefault(day, 0m);
                    var cost = costByDay.GetValueOrDefault(day, 0m);
                    points.Add(new DailyRevenuePointDto
                    {
                        Label = day.ToString("dd/MM"),
                        Revenue = rev,
                        Profit = rev - cost
                    });
                }

                return ApiResult<RevenueTrendDto>.Ok(new RevenueTrendDto { Points = points });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy xu hướng doanh thu.");
                return ApiResult<RevenueTrendDto>.Fail("Không thể tải dữ liệu xu hướng doanh thu.");
            }
        }

        public async Task<ApiResult<OrderChannelDto>> GetOrderChannelsAsync(DateTime from, DateTime to)
        {
            var (valid, error) = ValidateRange(from, to);
            if (!valid) return ApiResult<OrderChannelDto>.Fail(error);
            try
            {
                var fromDate = from.Date;
                var toDate = to.Date.AddDays(1);

                var result = await _context.Orders.AsNoTracking()
                    .Where(o => o.Status == 3 && o.OrderDate >= fromDate && o.OrderDate < toDate)
                    .GroupBy(o => o.OrderType)
                    .Select(g => new { Type = g.Key, Count = g.Count() })
                    .ToListAsync();

                return ApiResult<OrderChannelDto>.Ok(new OrderChannelDto
                {
                    OnlineCount = result.FirstOrDefault(x => x.Type == 0)?.Count ?? 0,
                    PosCount = result.FirstOrDefault(x => x.Type == 1)?.Count ?? 0
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thống kê kênh bán hàng.");
                return ApiResult<OrderChannelDto>.Fail("Không thể tải dữ liệu kênh bán hàng.");
            }
        }

        public async Task<ApiResult<List<TopProductDto>>> GetTopProductsAsync(DateTime from, DateTime to, int top)
        {
            var (valid, error) = ValidateRange(from, to);
            if (!valid) return ApiResult<List<TopProductDto>>.Fail(error);
            try
            {
                var fromDate = from.Date;
                var toDate = to.Date.AddDays(1);

                var query =
                    from o in _context.Orders
                    where o.Status == 3 && o.OrderDate >= fromDate && o.OrderDate < toDate
                    join od in _context.OrderDetails on o.Id equals od.OrderId
                    join v in _context.ProductVariants on od.VariantId equals v.Id
                    join p in _context.Products on v.ProductId equals p.Id
                    group od by new { od.VariantId, v.VariantName, p.Name } into g
                    orderby g.Sum(x => x.TotalLine) descending
                    select new TopProductDto
                    {
                        ProductName = g.Key.Name,
                        VariantName = g.Key.VariantName,
                        UnitsSold = g.Sum(x => x.Quantity),
                        Revenue = g.Sum(x => x.TotalLine)
                    };

                var data = await query.Take(top).ToListAsync();
                return ApiResult<List<TopProductDto>>.Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy top sản phẩm bán chạy.");
                return ApiResult<List<TopProductDto>>.Fail("Không thể tải dữ liệu sản phẩm bán chạy.");
            }
        }

        public async Task<ApiResult<List<CategoryRevenueDto>>> GetCategoryRevenueAsync(DateTime from, DateTime to, int top)
        {
            var (valid, error) = ValidateRange(from, to);
            if (!valid) return ApiResult<List<CategoryRevenueDto>>.Fail(error);
            try
            {
                var fromDate = from.Date;
                var toDate = to.Date.AddDays(1);

                var query =
                    from o in _context.Orders
                    where o.Status == 3 && o.OrderDate >= fromDate && o.OrderDate < toDate
                    join od in _context.OrderDetails on o.Id equals od.OrderId
                    join v in _context.ProductVariants on od.VariantId equals v.Id
                    join p in _context.Products on v.ProductId equals p.Id
                    join c in _context.Categories on p.CategoryId equals c.Id
                    group od by new { c.Id, c.Name } into g
                    orderby g.Sum(x => x.TotalLine) descending
                    select new CategoryRevenueDto
                    {
                        CategoryName = g.Key.Name,
                        OrderCount = g.Select(x => x.OrderId).Distinct().Count(),
                        UnitsSold = g.Sum(x => x.Quantity),
                        Revenue = g.Sum(x => x.TotalLine)
                    };

                var data = await query.Take(top).ToListAsync();
                return ApiResult<List<CategoryRevenueDto>>.Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy doanh thu theo danh mục.");
                return ApiResult<List<CategoryRevenueDto>>.Fail("Không thể tải dữ liệu doanh thu theo danh mục.");
            }
        }

        public async Task<ApiResult<InventorySummaryDto>> GetInventorySummaryAsync()
        {
            try
            {
                var statusCounts = await _context.ProductSerials.AsNoTracking()
                    .GroupBy(s => s.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToListAsync();

                var totalSkus = await _context.ProductVariants.AsNoTracking()
                    .Where(v => !v.IsDeleted)
                    .CountAsync();

                var lowStockSkus = await _context.ProductVariants.AsNoTracking()
                    .Where(v => !v.IsDeleted && v.StockQuantity <= 3 && v.StockQuantity >= 0)
                    .CountAsync();

                var dto = new InventorySummaryDto
                {
                    TotalSkus = totalSkus,
                    TotalAvailable = statusCounts.FirstOrDefault(x => x.Status == 0)?.Count ?? 0,
                    TotalReserved = statusCounts.FirstOrDefault(x => x.Status == 1)?.Count ?? 0,
                    TotalSold = statusCounts.FirstOrDefault(x => x.Status == 2)?.Count ?? 0,
                    TotalDefective = statusCounts.FirstOrDefault(x => x.Status == 3)?.Count ?? 0,
                    TotalReturned = statusCounts.FirstOrDefault(x => x.Status == 4)?.Count ?? 0,
                    LowStockSkus = lowStockSkus
                };
                return ApiResult<InventorySummaryDto>.Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thống kê kho hàng.");
                return ApiResult<InventorySummaryDto>.Fail("Không thể tải dữ liệu thống kê kho hàng.");
            }
        }
    }
}
