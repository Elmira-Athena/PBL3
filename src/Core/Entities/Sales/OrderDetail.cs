using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    // 3. OrderDetails
    [Table("OrderDetails")]
    public class OrderDetail
    {
        [Key]
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int VariantId { get; set; }

        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }

        // TotalLine is computed column
        public decimal TotalLine { get; set; }

        [ForeignKey("OrderId")]
        public virtual Order Order { get; set; } = null!;
        [ForeignKey("VariantId")]
        public virtual ProductVariant Variant { get; set; } = null!;

        public virtual ICollection<OrderSerial> OrderSerials { get; set; } = new List<OrderSerial>();
    }
}
