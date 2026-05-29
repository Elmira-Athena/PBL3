using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    [Table("ProductVariants")]
    public class ProductVariant
    {
        [Key]
        public int Id { get; set; }
        public int ProductId { get; set; }

        [Required]
        [MaxLength(50)]
        public string SKU { get; set; } = string.Empty;
        [Required]
        [MaxLength(200)]
        public string VariantName { get; set; } = string.Empty;
        [Required]
        [MaxLength(250)]
        public string Slug { get; set; } = string.Empty;

        public decimal Price { get; set; }
        public decimal? OriginalPrice { get; set; }
        public int WarrantyMonth { get; set; }
        public Dictionary<string, string> Specifications { get; set; } = new();

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        [MaxLength(100)]
        public string? CreatedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        [MaxLength(100)]
        public string? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedDate { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; } = null!;
        public virtual ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
        public virtual ICollection<ProductSerial> Serials { get; set; } = new List<ProductSerial>();

        /// <summary>
        /// Số lượng tồn kho — cột vật lý, được đồng bộ bởi InventorySyncService.
        /// KHÔNG tự đếm on-the-fly. Luồng Read chỉ đọc giá trị này.
        /// </summary>
        public int StockQuantity { get; set; }
    }
}
