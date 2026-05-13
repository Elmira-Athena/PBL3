namespace PBL3.Shared.DTOs.Analytics
{
    public class AnalyticsFilterRequest
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
    }

    public class AnalyticsSummaryDto
    {
        public decimal TotalRevenue { get; set; }
        public decimal GrossProfit { get; set; }
        public int TotalOrders { get; set; }
        public int CancelledOrders { get; set; }
        public decimal CancelRate { get; set; }
        public decimal AverageOrderValue { get; set; }
        public int NewCustomers { get; set; }

        public int PayCod { get; set; }
        public int PayBanking { get; set; }
        public int PayVnPay { get; set; }
    }

    public class DailyRevenuePointDto
    {
        public string Label { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public decimal Profit { get; set; }
    }

    public class RevenueTrendDto
    {
        public List<DailyRevenuePointDto> Points { get; set; } = new();
    }

    public class OrderChannelDto
    {
        public int OnlineCount { get; set; }
        public int PosCount { get; set; }
    }

    public class TopProductDto
    {
        public string ProductName { get; set; } = string.Empty;
        public string VariantName { get; set; } = string.Empty;
        public int UnitsSold { get; set; }
        public decimal Revenue { get; set; }
    }

    public class CategoryRevenueDto
    {
        public string CategoryName { get; set; } = string.Empty;
        public int OrderCount { get; set; }
        public int UnitsSold { get; set; }
        public decimal Revenue { get; set; }
    }

    public class InventorySummaryDto
    {
        public int TotalSkus { get; set; }
        public int TotalAvailable { get; set; }
        public int TotalReserved { get; set; }
        public int TotalSold { get; set; }
        public int TotalDefective { get; set; }
        public int TotalReturned { get; set; }
        public int LowStockSkus { get; set; }
    }
}
