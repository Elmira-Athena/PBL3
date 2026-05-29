using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    [Table("Quotations")]
    public class Quotation
    {
        [Key]
        public int Id { get; set; }

        public int TicketId { get; set; }
        public DateTime IssuedDate { get; set; } = DateTime.UtcNow;
        public Guid IssuedByEmployeeId { get; set; }

        public decimal LaborCost { get; set; }
        public decimal PartsTotal { get; set; }
        public decimal GrandTotal { get; set; }

        public byte Status { get; set; } = 0; // QuotationStatus.Pending

        [MaxLength(1000)]
        public string? CustomerDecisionNote { get; set; }

        public DateTime? CustomerDecidedAt { get; set; }

        [ForeignKey("TicketId")]
        public virtual ServiceTicket Ticket { get; set; } = null!;

        public virtual ICollection<QuotationItem> Items { get; set; } = new List<QuotationItem>();
    }
}
