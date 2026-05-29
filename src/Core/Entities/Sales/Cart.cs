using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    // 5. Carts
    [Table("Carts")]
    public class Cart
    {
        [Key]
        public int Id { get; set; }
        public Guid UserId { get; set; }
        public int VariantId { get; set; }
        public int Quantity { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [ForeignKey("UserId")]
        public virtual AppUser User { get; set; } = null!;
        [ForeignKey("VariantId")]
        public virtual ProductVariant Variant { get; set; } = null!;
    }
}
