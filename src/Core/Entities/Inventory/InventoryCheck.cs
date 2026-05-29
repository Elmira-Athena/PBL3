using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
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
}
