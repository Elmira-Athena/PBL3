namespace PBL3.Shared.DTOs.ServiceTickets
{
    public class QuotationDetailDto
    {
        public int Id { get; set; }
        public int TicketId { get; set; }
        public DateTime IssuedDate { get; set; }

        public decimal LaborCost { get; set; }
        public decimal PartsTotal { get; set; }
        public decimal GrandTotal { get; set; }

        public byte Status { get; set; }
        public string StatusLabel { get; set; } = string.Empty;

        public DateTime? CustomerDecidedAt { get; set; }
        public string? CustomerDecisionNote { get; set; }

        public List<QuotationItemDto> Items { get; set; } = new();
    }
}
