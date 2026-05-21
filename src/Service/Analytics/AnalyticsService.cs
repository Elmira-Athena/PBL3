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

        /// <summary>
        /// NGHIỆP VỤ: Truy vấn báo cáo tổng quan tình hình kinh doanh trong một khoảng thời gian (doanh thu, lợi nhuận gộp, đơn hàng).
        /// ĐẶC BIỆT (Traceable Cost Price): Hệ thống thực hiện tính toán giá vốn thực tế (totalCost) một cách cực kỳ chuẩn xác:
        /// Kết nối trực tiếp giữa: Đơn hàng hoàn thành (Status = 3) -> Chi tiết đơn hàng (od) -> Cặp Serial đã bán (os) -> Mã định danh vật lý (ps) 
        /// -> Hóa đơn nhập hàng gốc (ird) dựa trên sự kết hợp khóa ngoại (ImportReceiptId + VariantId).
        /// Cơ chế này giúp cửa hàng truy vết chính xác giá vốn thực tế tại thời điểm nhập của từng thiết bị đã bán ra,
        /// từ đó tính toán Lợi nhuận gộp (Gross Profit = Revenue - Cost) chuẩn chỉ theo phương pháp Specific Identification (Định danh thực tế),
        /// giúp ban quản trị đánh giá chính xác hiệu suất tài chính thực tế.
        /// </summary>
        public async Task<ApiResult<AnalyticsSummaryDto>> GetSummaryAsync(DateTime from, DateTime to)
        {
            var (valid, error) = ValidateRange(from, to);
            if (!valid) return ApiResult<AnalyticsSummaryDto>.Fail(error);
            try
            {
                var fromDate = from.Date;
                var toDate = to.Date.AddDays(1);

                // Doanh thu thực tế phát sinh từ các đơn hàng thành công (Status = 3: Đã giao hàng/Thanh toán)
                var successQ = _context.Orders.AsNoTracking()
                    .Where(o => o.Status == 3 && o.OrderDate >= fromDate && o.OrderDate < toDate);

                var totalRevenue = await successQ.SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;
                var totalOrders = await successQ.CountAsync();

                // TRUY VẾT GIÁ VỐN THỰC TẾ: Đối chiếu từng Serial đã bán ra để lấy giá nhập gốc của đúng lô hàng nhập
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

        /// <summary>
        /// NGHIỆP VỤ: Phân tích xu hướng biến động doanh thu và lợi nhuận thực tế theo từng ngày.
        /// Thống kê chi tiết biểu đồ thu nhập - chi phí hàng ngày bằng cách ánh xạ doanh số bán ra
        /// và truy vết giá vốn gốc của các serial tương ứng, giúp ban quản trị theo dõi trực quan
        /// chu kỳ phát triển doanh số.
        /// </summary>
        public async Task<ApiResult<RevenueTrendDto>> GetRevenueTrendAsync(DateTime from, DateTime to)
        {
            var (valid, error) = ValidateRange(from, to);
            if (!valid) return ApiResult<RevenueTrendDto>.Fail(error);
            try
            {
                var fromDate = from.Date;
                var toDate = to.Date.AddDays(1);

                // Doanh thu thực tế theo từng ngày trong khoảng thời gian chọn
                var revenueByDay = await _context.Orders.AsNoTracking()
                    .Where(o => o.Status == 3 && o.OrderDate >= fromDate && o.OrderDate < toDate)
                    .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month, o.OrderDate.Day })
                    .Select(g => new { g.Key, Revenue = g.Sum(o => o.TotalAmount) })
                    .ToDictionaryAsync(
                        x => new DateTime(x.Key.Year, x.Key.Month, x.Key.Day),
                        x => x.Revenue);

                // Giá vốn nhập gốc tương ứng của các sản phẩm được bán thành công trong từng ngày
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

        /// <summary>
        /// NGHIỆP VỤ: Phân tích và thống kê sản lượng đơn hàng thành công phân bổ theo Kênh Bán Hàng.
        /// Chia làm 2 kênh chính: Online (Đơn hàng khách tự đặt trên website) và POS (Đơn hàng bán lẻ trực tiếp tại quầy).
        /// Hỗ trợ ban giám đốc đánh giá hiệu năng của từng kênh phân phối để điều chỉnh ngân sách tiếp thị và nhân sự.
        /// </summary>
        public async Task<ApiResult<OrderChannelDto>> GetOrderChannelsAsync(DateTime from, DateTime to)
        {
            var (valid, error) = ValidateRange(from, to);
            if (!valid) return ApiResult<OrderChannelDto>.Fail(error);
            try
            {
                var fromDate = from.Date;
                var toDate = to.Date.AddDays(1);

                // Truy vấn cơ sở dữ liệu để đếm số lượng đơn hàng thành công theo từng loại hình kênh (0 = Online, 1 = POS)
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

        /// <summary>
        /// NGHIỆP VỤ: Truy vấn danh sách các Sản phẩm/Biến thể Bán chạy nhất (Top-Selling Products) trong khoảng thời gian chọn.
        /// Tính toán tổng doanh thu và tổng số lượng sản phẩm bán ra thực tế dựa trên các đơn hàng thành công.
        /// Hỗ trợ thủ kho và ban quản trị xác định sản phẩm "Key" mang lại dòng tiền lớn nhất để có kế hoạch trữ hàng tối ưu.
        /// </summary>
        public async Task<ApiResult<List<TopProductDto>>> GetTopProductsAsync(DateTime from, DateTime to, int top)
        {
            var (valid, error) = ValidateRange(from, to);
            if (!valid) return ApiResult<List<TopProductDto>>.Fail(error);
            try
            {
                var fromDate = from.Date;
                var toDate = to.Date.AddDays(1);

                // Phép JOIN đa bảng nhóm theo biến thể sản phẩm, sắp xếp giảm dần theo tổng doanh thu (TotalLine)
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
                        UnitsSold = g.Sum(x => x.Quantity), // Tổng số lượng sản phẩm thực bán
                        Revenue = g.Sum(x => x.TotalLine) // Tổng doanh thu thu về (đã trừ chiết khấu dòng nếu có)
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

        /// <summary>
        /// NGHIỆP VỤ: Phân tích cơ cấu doanh thu theo Danh mục Sản phẩm (Category Revenue Distribution).
        /// Tính toán tổng doanh thu, tổng sản lượng bán và số lượng đơn hàng phát sinh độc lập đối với từng danh mục hàng hóa.
        /// Giúp ban giám đốc nhận diện danh mục sản phẩm nào đang là thế mạnh của cửa hàng (ví dụ: Laptop, Điện thoại, Phụ kiện).
        /// </summary>
        public async Task<ApiResult<List<CategoryRevenueDto>>> GetCategoryRevenueAsync(DateTime from, DateTime to, int top)
        {
            var (valid, error) = ValidateRange(from, to);
            if (!valid) return ApiResult<List<CategoryRevenueDto>>.Fail(error);
            try
            {
                var fromDate = from.Date;
                var toDate = to.Date.AddDays(1);

                // Phép JOIN nhóm dữ liệu theo danh mục (Category) từ chi tiết đơn hàng
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
                        OrderCount = g.Select(x => x.OrderId).Distinct().Count(), // Số lượng đơn hàng độc lập có chứa sản phẩm danh mục này
                        UnitsSold = g.Sum(x => x.Quantity), // Tổng số lượng sản phẩm bán ra thuộc danh mục
                        Revenue = g.Sum(x => x.TotalLine) // Tổng doanh thu tích lũy của danh mục
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

        /// <summary>
        /// NGHIỆP VỤ: Kết xuất báo cáo sức khỏe kho hàng và phân tích phân bổ trạng thái vật lý của Serial.
        /// Thống kê chi tiết số lượng sản phẩm theo từng giai đoạn vòng đời Serial:
        /// 1. Available (0): Sẵn sàng bán tại cửa hàng hoặc online.
        /// 2. Reserved (1): Đơn hàng online đã tạo đang tạm khóa giữ hàng chờ thanh toán.
        /// 3. Sold (2): Đã bàn giao cho khách hàng (kết thúc vòng tồn kho).
        /// 4. Defective (3): Hàng phát hiện lỗi hỏng đang lưu kho chờ bảo hành/RMA.
        /// 5. Returned (4): Thiết bị lỗi đã hoàn trả thành công về nhà cung cấp.
        /// Đồng thời đưa ra cảnh báo số lượng biến thể có mức tồn kho báo động (Low Stock Skus <= 3) để thủ kho lên kế hoạch nhập hàng.
        /// </summary>
        public async Task<ApiResult<InventorySummaryDto>> GetInventorySummaryAsync()
        {
            try
            {
                // Thống kê số lượng serial hiện hành theo từng nhóm trạng thái
                var statusCounts = await _context.ProductSerials.AsNoTracking()
                    .GroupBy(s => s.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToListAsync();

                var totalSkus = await _context.ProductVariants.AsNoTracking()
                    .Where(v => !v.IsDeleted)
                    .CountAsync();

                // Cảnh báo các biến thể sản phẩm có số lượng tồn kho thực tế ở mức thấp báo động (tồn kho từ 0 đến 3 sản phẩm)
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
