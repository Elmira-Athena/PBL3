using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    [Table("ServiceInvoiceItems")]
    public class ServiceInvoiceItem
    {
        [Key]
        public int Id { get; set; }

        public int InvoiceId { get; set; }
        public int? VariantId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Description { get; set; } = string.Empty;

        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }

        [ForeignKey("InvoiceId")]
        public virtual ServiceInvoice Invoice { get; set; } = null!;

        [ForeignKey("VariantId")]
        public virtual ProductVariant? Variant { get; set; }
    }
}
