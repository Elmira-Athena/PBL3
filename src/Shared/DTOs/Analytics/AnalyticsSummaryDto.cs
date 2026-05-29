namespace PBL3.Shared.DTOs.Analytics
{
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
}
