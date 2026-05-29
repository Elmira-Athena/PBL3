using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    // 6. InventoryCheckDetails
    [Table("InventoryCheckDetails")]
    public class InventoryCheckDetail
    {
        [Key]
        public int Id { get; set; }
        public int CheckId { get; set; }
        public int VariantId { get; set; }

        public int SystemQuantity { get; set; }
        public int ActualQuantity { get; set; }
        public int Difference { get; set; }

        public int MatchedQuantity { get; set; }
        public int MissingQuantity { get; set; }
        public int SurplusQuantity { get; set; }
        public int DefectiveQuantity { get; set; }

        [MaxLength(255)]
        public string? Reason { get; set; }

        [ForeignKey("CheckId")]
        public virtual InventoryCheck Check { get; set; } = null!;
        [ForeignKey("VariantId")]
        public virtual ProductVariant Variant { get; set; } = null!;

        public virtual ICollection<InventoryCheckDetailSerial> DetailSerials { get; set; } = new List<InventoryCheckDetailSerial>();
    }
}
