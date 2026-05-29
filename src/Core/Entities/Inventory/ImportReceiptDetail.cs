using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
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
}
