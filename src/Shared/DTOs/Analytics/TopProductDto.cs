namespace PBL3.Shared.DTOs.Analytics
{
    public class TopProductDto
    {
        public string ProductName { get; set; } = string.Empty;
        public string VariantName { get; set; } = string.Empty;
        public int UnitsSold { get; set; }
        public decimal Revenue { get; set; }
    }
}
