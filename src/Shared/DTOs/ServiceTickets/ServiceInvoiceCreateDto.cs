namespace PBL3.Shared.DTOs.ServiceTickets
{
    public class ServiceInvoiceCreateDto
    {
        public byte PaymentMethod { get; set; } // 0=Cash, 1=Banking, 2=VNPay
        public string? Note { get; set; }
    }
}
