namespace PBL3.Shared.DTOs.ServiceTickets
{
    public class ServiceInvoiceListDto
    {
        public int Id { get; set; }
        public string InvoiceCode { get; set; } = string.Empty;
        public int TicketId { get; set; }
        public DateTime IssuedDate { get; set; }
        public decimal GrandTotal { get; set; }
        public byte PaymentStatus { get; set; }
        public string PaymentStatusLabel { get; set; } = string.Empty;
    }
}
