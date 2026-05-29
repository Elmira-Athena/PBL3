namespace PBL3.Shared.DTOs.ServiceTickets
{
    public class ServiceInvoiceDetailDto
    {
        public int Id { get; set; }
        public string InvoiceCode { get; set; } = string.Empty;
        public int TicketId { get; set; }
        public int? QuotationId { get; set; }
        public Guid? IssuedByEmployeeId { get; set; }

        public DateTime IssuedDate { get; set; }

        public decimal LaborCost { get; set; }
        public decimal PartsTotal { get; set; }
        public decimal GrandTotal { get; set; }

        public byte PaymentMethod { get; set; } // 0=Cash, 1=Banking, 2=VNPay
        public string PaymentMethodLabel { get; set; } = string.Empty;

        public byte PaymentStatus { get; set; } // 0=Unpaid, 1=Paid
        public string PaymentStatusLabel { get; set; } = string.Empty;

        public string? Note { get; set; }

        public List<ServiceInvoiceItemDto> Items { get; set; } = new();
    }
}
