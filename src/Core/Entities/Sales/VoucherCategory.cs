using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    // 1b. VoucherCategories (danh mục sản phẩm được áp dụng voucher)
    [Table("VoucherCategories")]
    public class VoucherCategory
    {
        public int VoucherId { get; set; }
        public int CategoryId { get; set; }

        [ForeignKey("VoucherId")]
        public virtual Voucher Voucher { get; set; } = null!;
        [ForeignKey("CategoryId")]
        public virtual Category Category { get; set; } = null!;
    }
}
