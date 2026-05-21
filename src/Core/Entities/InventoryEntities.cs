using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    // 1. Suppliers
    [Table("Suppliers")]
    public class Supplier
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        [MaxLength(100)]
        public string? ContactPerson { get; set; }
        [Required]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;
        [MaxLength(100)]
        public string? Email { get; set; }
        [MaxLength(255)]
        public string? Address { get; set; }
        [MaxLength(20)]
        public string? TaxCode { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }

        public virtual ICollection<ImportReceipt> ImportReceipts { get; set; } = new List<ImportReceipt>();
    }

    // 2. ImportReceipts
    [Table("ImportReceipts")]
    public class ImportReceipt
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [MaxLength(20)]
        public string ReceiptCode { get; set; } = string.Empty;

        public int SupplierId { get; set; }
        public Guid EmployeeId { get; set; }

        public DateTime ImportDate { get; set; } = DateTime.UtcNow;
        public decimal TotalAmount { get; set; }
        [MaxLength(500)]
        public string? Note { get; set; }
        public bool IsDeleted { get; set; }

        [ForeignKey("SupplierId")]
        public virtual Supplier Supplier { get; set; } = null!;

        public virtual ICollection<ImportReceiptDetail> Details { get; set; } = new List<ImportReceiptDetail>();
    }

    // 3. ImportReceiptDetails
    [Table("ImportReceiptDetails")]
    public class ImportReceiptDetail
    {
        [Key]
        public int Id { get; set; }
        public int ReceiptId { get; set; }
        public int VariantId { get; set; }

        public int Quantity { get; set; }
        public decimal ImportPrice { get; set; }

        [ForeignKey("ReceiptId")]
        public virtual ImportReceipt Receipt { get; set; } = null!;
        [ForeignKey("VariantId")]
        public virtual ProductVariant Variant { get; set; } = null!;
    }

    // 4. ProductSerials
    [Table("ProductSerials")]
    public class ProductSerial
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [MaxLength(100)]
        public string SerialNumber { get; set; } = string.Empty;

        public int VariantId { get; set; }
        public int ImportReceiptId { get; set; }

        // 0: Available, 1: Reserved, 2: Sold, 3: Defective, 4: Returned, 5: Lost
        public byte Status { get; set; }

        public int? OrderId { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? SoldDate { get; set; }

        [ForeignKey("VariantId")]
        public virtual ProductVariant Variant { get; set; } = null!;
        [ForeignKey("ImportReceiptId")]
        public virtual ImportReceipt ImportReceipt { get; set; } = null!;
    }

    // 5. InventoryChecks
    [Table("InventoryChecks")]
    public class InventoryCheck
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string CheckCode { get; set; } = string.Empty;

        public Guid EmployeeId { get; set; }

        public DateTime CheckDate { get; set; } = DateTime.UtcNow;

        /// <summary>Thời điểm chốt snapshot chính xác (= CheckDate khi tạo phiếu).</summary>
        public DateTime SnapshotAt { get; set; } = DateTime.UtcNow;

        [MaxLength(500)]
        public string? Note { get; set; }

        // 0: Draft, 1: AwaitingApproval, 2: Completed, 3: Cancelled
        public byte Status { get; set; }

        // 0: AllStore, 1: Category
        public byte ScopeType { get; set; }

        public int? ScopeCategoryId { get; set; }

        public Guid? ApprovedByEmployeeId { get; set; }
        public DateTime? ApprovedAt { get; set; }

        [MaxLength(500)]
        public string? RejectReason { get; set; }

        public bool IsDeleted { get; set; }

        [ForeignKey("ScopeCategoryId")]
        public virtual Category? ScopeCategory { get; set; }

        public virtual ICollection<InventoryCheckDetail> Details { get; set; } = new List<InventoryCheckDetail>();
        public virtual ICollection<InventoryCheckDetailSerial> DetailSerials { get; set; } = new List<InventoryCheckDetailSerial>();
    }

    // 6. InventoryCheckDetails
    [Table("InventoryCheckDetails")]
    public class InventoryCheckDetail
    {
        [Key]
        public int Id { get; set; }
        public int CheckId { get; set; }
        public int VariantId { get; set; }

        public int SystemQuantity { get; set; }
        public int ActualQuantity { get; set; }
        public int Difference { get; set; }

        public int MatchedQuantity { get; set; }
        public int MissingQuantity { get; set; }
        public int SurplusQuantity { get; set; }
        public int DefectiveQuantity { get; set; }

        [MaxLength(255)]
        public string? Reason { get; set; }

        [ForeignKey("CheckId")]
        public virtual InventoryCheck Check { get; set; } = null!;
        [ForeignKey("VariantId")]
        public virtual ProductVariant Variant { get; set; } = null!;

        public virtual ICollection<InventoryCheckDetailSerial> DetailSerials { get; set; } = new List<InventoryCheckDetailSerial>();
    }

    // 7. InventoryCheckDetailSerials — serial-level audit log
    [Table("InventoryCheckDetailSerials")]
    public class InventoryCheckDetailSerial
    {
        [Key]
        public int Id { get; set; }

        public int CheckId { get; set; }

        // Nullable: UnknownSurplus chưa biết variant → DetailId = null
        public int? DetailId { get; set; }

        // Nullable: UnknownSurplus chưa pick variant
        public int? VariantId { get; set; }

        // Nullable: UnknownSurplus không tồn tại trong DB
        public int? SerialId { get; set; }

        [Required]
        [MaxLength(100)]
        public string SerialNumberRaw { get; set; } = string.Empty;

        // SerialStatus tại thời điểm chốt snapshot hoặc quét; null = UnknownSurplus
        public byte? OriginalStatus { get; set; }

        // 0: Pending, 1: Matched, 2: Missing, 3: Surplus, 4: UnknownSurplus, 5: Defective
        public byte ScanStatus { get; set; }

        public DateTime? ScannedAt { get; set; }
        public Guid? ScannedByEmployeeId { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }

        [MaxLength(200)]
        public string? ProposedActionNote { get; set; }

        /// <summary>
        /// true nếu serial được nghiệp vụ tự động giải quyết trong cửa sổ kiểm kê
        /// (vd: serial Missing nhưng thực ra đã được bán trong thời gian kiểm kê).
        /// </summary>
        public bool ResolvedDuringApproval { get; set; }

        [ForeignKey("CheckId")]
        public virtual InventoryCheck Check { get; set; } = null!;

        [ForeignKey("DetailId")]
        public virtual InventoryCheckDetail? Detail { get; set; }

        [ForeignKey("VariantId")]
        public virtual ProductVariant? Variant { get; set; }

        [ForeignKey("SerialId")]
        public virtual ProductSerial? Serial { get; set; }
    }

    // 8. InventoryAdjustmentLogs — bút toán điều chỉnh kho phục vụ kế toán
    [Table("InventoryAdjustmentLogs")]
    public class InventoryAdjustmentLog
    {
        [Key]
        public int Id { get; set; }

        public int AuditCheckId { get; set; }
        public int SerialId { get; set; }
        public int VariantId { get; set; }

        public byte OldStatus { get; set; }
        public byte NewStatus { get; set; }

        // 1: Lost, 2: Defective
        public byte AdjustmentType { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CostImpact { get; set; }

        [MaxLength(500)]
        public string? Reason { get; set; }

        public DateTime AdjustedDate { get; set; } = DateTime.UtcNow;
        public Guid AdjustedByEmployeeId { get; set; }

        [ForeignKey("AuditCheckId")]
        public virtual InventoryCheck AuditCheck { get; set; } = null!;

        [ForeignKey("SerialId")]
        public virtual ProductSerial Serial { get; set; } = null!;

        [ForeignKey("VariantId")]
        public virtual ProductVariant Variant { get; set; } = null!;
    }
}
