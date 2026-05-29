using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
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
