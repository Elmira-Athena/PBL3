using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    // 6. VoucherUsages (Bảng trung gian: User đã dùng Voucher nào, trong Order nào)
    [Table("VoucherUsages")]
    public class VoucherUsage
    {
        [Key]
        public int Id { get; set; }

        public int VoucherId { get; set; }
        public Guid UserId { get; set; }
        public int OrderId { get; set; }

        /// <summary>
        /// Số tiền thực tế được giảm bởi voucher này trong đơn hàng.
        /// VD: Voucher giảm 20%, MaxDiscount = 50k, đơn 300k -> DiscountApplied = 50k.
        /// </summary>
        public decimal DiscountApplied { get; set; }

        public DateTime UsedDate { get; set; } = DateTime.UtcNow;

        [ForeignKey("VoucherId")]
        public virtual Voucher Voucher { get; set; } = null!;
        [ForeignKey("UserId")]
        public virtual AppUser User { get; set; } = null!;
        [ForeignKey("OrderId")]
        public virtual Order Order { get; set; } = null!;
    }
}
