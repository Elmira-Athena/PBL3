using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
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
}
