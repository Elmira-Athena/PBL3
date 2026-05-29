using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    [Table("QuotationItems")]
    public class QuotationItem
    {
        [Key]
        public int Id { get; set; }

        public int QuotationId { get; set; }
        public int? VariantId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Description { get; set; } = string.Empty;

        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }

        [ForeignKey("QuotationId")]
        public virtual Quotation Quotation { get; set; } = null!;

        [ForeignKey("VariantId")]
        public virtual ProductVariant? Variant { get; set; }
    }
}
