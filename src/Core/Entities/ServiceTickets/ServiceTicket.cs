using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    [Table("ServiceTickets")]
    public class ServiceTicket
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string TicketCode { get; set; } = string.Empty;

        public int SerialId { get; set; }
        public int OriginalOrderId { get; set; }
        public Guid? CustomerId { get; set; }

        public DateTime IntakeDate { get; set; } = DateTime.UtcNow;
        public Guid IntakeEmployeeId { get; set; }

        public bool HasScratches { get; set; }
        public bool HasDents { get; set; }
        public bool HasBurnMarks { get; set; }
        public bool HasMissingAccessories { get; set; }

        [MaxLength(1000)]
        public string? CosmeticNotes { get; set; }

        [MaxLength(2000)]
        public string CustomerReportedIssue { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? WalkInCustomerName { get; set; }

        [MaxLength(20)]
        public string? WalkInCustomerPhone { get; set; }

        public bool WasInWarrantyAtIntake { get; set; }
        public DateTime? WarrantyEndDateAtIntake { get; set; }
        public byte WarrantyEvalSource { get; set; }

        public byte Status { get; set; } = 0; // ServiceTicketStatus.Received
        public byte ResolutionType { get; set; } = 0; // TicketResolutionType.Pending
        public Guid? AssignedEmployeeId { get; set; }

        [MaxLength(2000)]
        public string? DiagnosisFindings { get; set; }

        public DateTime? DiagnosedAt { get; set; }
        public Guid? DiagnosedByEmployeeId { get; set; }

        public int? ReplacementSerialId { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? ModifiedDate { get; set; }
        public DateTime? CompletedDate { get; set; }

        [MaxLength(500)]
        public string? CancelReason { get; set; }

        public DateTime? CancelledAt { get; set; }
        public bool IsDeleted { get; set; }

        [ForeignKey("SerialId")]
        public virtual ProductSerial Serial { get; set; } = null!;

        [ForeignKey("OriginalOrderId")]
        public virtual Order OriginalOrder { get; set; } = null!;

        [ForeignKey("CustomerId")]
        public virtual AppUser? Customer { get; set; }

        [ForeignKey("ReplacementSerialId")]
        public virtual ProductSerial? ReplacementSerial { get; set; }

        public virtual ICollection<ServiceTicketStatusHistory> StatusHistory { get; set; } = new List<ServiceTicketStatusHistory>();
        public virtual ICollection<Quotation> Quotations { get; set; } = new List<Quotation>();
        public virtual RmaShipment? RmaShipment { get; set; }
        public virtual ServiceInvoice? Invoice { get; set; }
    }
}
