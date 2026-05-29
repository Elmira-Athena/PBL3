namespace PBL3.Shared.DTOs.Analytics
{
    public class CategoryRevenueDto
    {
        public string CategoryName { get; set; } = string.Empty;
        public int OrderCount { get; set; }
        public int UnitsSold { get; set; }
        public decimal Revenue { get; set; }
    }
}
