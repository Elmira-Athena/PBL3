using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    [Table("ServiceInvoices")]
    public class ServiceInvoice
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string InvoiceCode { get; set; } = string.Empty;

        public int TicketId { get; set; }
        public int? QuotationId { get; set; }

        public DateTime IssuedDate { get; set; } = DateTime.UtcNow;
        public Guid IssuedByEmployeeId { get; set; }

        public decimal LaborCost { get; set; }
        public decimal PartsTotal { get; set; }
        public decimal GrandTotal { get; set; }

        public byte PaymentMethod { get; set; }
        public byte PaymentStatus { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }

        public bool IsDeleted { get; set; }

        [ForeignKey("TicketId")]
        public virtual ServiceTicket Ticket { get; set; } = null!;

        [ForeignKey("QuotationId")]
        public virtual Quotation? Quotation { get; set; }

        public virtual ICollection<ServiceInvoiceItem> Items { get; set; } = new List<ServiceInvoiceItem>();
    }
}
