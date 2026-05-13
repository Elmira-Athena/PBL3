namespace PBL3.Shared.DTOs.ServiceTickets
{
    public class QuotationCreateDto
    {
        public decimal LaborCost { get; set; }
        public List<QuotationItemCreateDto> Items { get; set; } = new();
    }

    public class QuotationItemCreateDto
    {
        public int? VariantId { get; set; }
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
    }

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

    public class QuotationItemDto
    {
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class QuotationAcceptDto
    {
        public byte NextStatus { get; set; } // 4=WaitingParts, 5=InRepair
    }

    public class QuotationRejectDto
    {
        public string Reason { get; set; } = string.Empty;
    }
}
