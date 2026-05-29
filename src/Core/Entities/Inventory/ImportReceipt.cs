using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    // 2. ImportReceipts
    [Table("ImportReceipts")]
    public class ImportReceipt
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [MaxLength(20)]
        public string ReceiptCode { get; set; } = string.Empty;

        public int SupplierId { get; set; }
        public Guid EmployeeId { get; set; }

        public DateTime ImportDate { get; set; } = DateTime.UtcNow;
        public decimal TotalAmount { get; set; }
        [MaxLength(500)]
        public string? Note { get; set; }
        public bool IsDeleted { get; set; }

        [ForeignKey("SupplierId")]
        public virtual Supplier Supplier { get; set; } = null!;

        public virtual ICollection<ImportReceiptDetail> Details { get; set; } = new List<ImportReceiptDetail>();
    }
}
