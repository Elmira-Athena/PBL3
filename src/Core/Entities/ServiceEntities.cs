using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities;

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

    /// <summary>
    /// Concurrency token do SQL Server tự sinh. Xem giải thích đầy đủ + <b>hai giới hạn</b>
    /// ở <see cref="ProductSerial.RowVersion"/> — đặc biệt: <c>ExecuteUpdateAsync</c> bỏ qua nó.
    /// </summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }

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

    /// <summary>
    /// Concurrency token do SQL Server tự sinh. Xem giải thích đầy đủ + <b>hai giới hạn</b>
    /// ở <see cref="ProductSerial.RowVersion"/> — đặc biệt: <c>ExecuteUpdateAsync</c> bỏ qua nó.
    /// </summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    [ForeignKey("TicketId")]
    public virtual ServiceTicket Ticket { get; set; } = null!;

    public virtual ICollection<QuotationItem> Items { get; set; } = new List<QuotationItem>();
}

[Table("QuotationItems")]
public class QuotationItem
{
    [Key]
    public int Id { get; set; }

    public int QuotationId { get; set; }
    public int? VariantId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    [ForeignKey("QuotationId")]
    public virtual Quotation Quotation { get; set; } = null!;

    [ForeignKey("VariantId")]
    public virtual ProductVariant? Variant { get; set; }
}

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

    /// <summary>
    /// Concurrency token do SQL Server tự sinh. Xem giải thích đầy đủ + <b>hai giới hạn</b>
    /// ở <see cref="ProductSerial.RowVersion"/> — đặc biệt: <c>ExecuteUpdateAsync</c> bỏ qua nó.
    /// </summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    [ForeignKey("TicketId")]
    public virtual ServiceTicket Ticket { get; set; } = null!;
}

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

[Table("ServiceInvoiceItems")]
public class ServiceInvoiceItem
{
    [Key]
    public int Id { get; set; }

    public int InvoiceId { get; set; }
    public int? VariantId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    [ForeignKey("InvoiceId")]
    public virtual ServiceInvoice Invoice { get; set; } = null!;

    [ForeignKey("VariantId")]
    public virtual ProductVariant? Variant { get; set; }
}

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
