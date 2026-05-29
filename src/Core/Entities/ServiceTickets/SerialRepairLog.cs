using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    [Table("SerialRepairLogs")]
    public class SerialRepairLog
    {
        [Key]
        public int Id { get; set; }

        public int SerialId { get; set; }
        public int? TicketId { get; set; }
        public byte ResolutionType { get; set; }
        public DateTime LoggedAt { get; set; } = DateTime.UtcNow;
        public Guid LoggedByEmployeeId { get; set; }

        [MaxLength(2000)]
        public string Summary { get; set; } = string.Empty;

        public int? ReplacedBySerialId { get; set; }

        [ForeignKey("SerialId")]
        public virtual ProductSerial Serial { get; set; } = null!;

        [ForeignKey("TicketId")]
        public virtual ServiceTicket? Ticket { get; set; }

        [ForeignKey("ReplacedBySerialId")]
        public virtual ProductSerial? ReplacedBySerial { get; set; }
    }
}
