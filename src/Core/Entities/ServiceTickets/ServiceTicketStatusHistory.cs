using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    [Table("ServiceTicketStatusHistory")]
    public class ServiceTicketStatusHistory
    {
        [Key]
        public int Id { get; set; }

        public int TicketId { get; set; }
        public byte FromStatus { get; set; }
        public byte ToStatus { get; set; }
        public Guid ChangedByEmployeeId { get; set; }
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(500)]
        public string? Note { get; set; }

        [ForeignKey("TicketId")]
        public virtual ServiceTicket Ticket { get; set; } = null!;
    }
}
