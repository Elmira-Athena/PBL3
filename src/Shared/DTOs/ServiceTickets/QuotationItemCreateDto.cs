namespace PBL3.Shared.DTOs.ServiceTickets
{
    public class QuotationItemCreateDto
    {
        public int? VariantId { get; set; }
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
    }
}
