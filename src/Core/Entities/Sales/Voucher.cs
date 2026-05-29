using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    // 1. Vouchers
    [Table("Vouchers")]
    public class Voucher
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        public byte DiscountType { get; set; } // 0: Amount, 1: Percentage
        public decimal DiscountValue { get; set; }

        public decimal MinOrderValue { get; set; }
        public decimal? MaxDiscountAmount { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public int? Quantity { get; set; } // null = không giới hạn số lượng
        public int UsedCount { get; set; }

        public bool IsActive { get; set; } = true;

        // Quản lý nâng cao
        public int? MaxUsesPerUser { get; set; }        // null = không giới hạn lần dùng/người
        public byte ApplyFor { get; set; }              // 0: Both, 1: Online, 2: POS
        public bool IsStackable { get; set; }           // false = không được dùng chung voucher khác
        [MaxLength(500)]
        public string? Description { get; set; }

        // Soft delete & audit
        public DateTime CreatedDate { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedDate { get; set; }

        public virtual ICollection<VoucherUsage> VoucherUsages { get; set; } = new List<VoucherUsage>();
        public virtual ICollection<VoucherCategory> VoucherCategories { get; set; } = new List<VoucherCategory>();
    }
}
