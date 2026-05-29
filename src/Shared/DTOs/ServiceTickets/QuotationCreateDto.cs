namespace PBL3.Shared.DTOs.ServiceTickets
{
    public class QuotationCreateDto
    {
        public decimal LaborCost { get; set; }
        public List<QuotationItemCreateDto> Items { get; set; } = new();
    }
}
