using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    [Table("RmaShipments")]
    public class RmaShipment
    {
        [Key]
        public int Id { get; set; }

        public int TicketId { get; set; }

        [Required]
        [MaxLength(100)]
        public string CarrierName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string TrackingCode { get; set; } = string.Empty;

        public DateTime ShippedDate { get; set; }
        public Guid ShippedByEmployeeId { get; set; }

        public DateTime? ReceivedBackDate { get; set; }
        public Guid? ReceivedByEmployeeId { get; set; }

        public byte ManufacturerResolution { get; set; } = 0; // ManufacturerResolution.Pending

        [MaxLength(1000)]
        public string? ManufacturerNotes { get; set; }

        [ForeignKey("TicketId")]
        public virtual ServiceTicket Ticket { get; set; } = null!;
    }
}
