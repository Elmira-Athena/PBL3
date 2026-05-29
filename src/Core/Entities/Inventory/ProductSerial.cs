using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
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
}
