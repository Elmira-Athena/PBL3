using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    // 1. Manufacturers
    [Table("Manufacturers")]
    public class Manufacturer
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        [MaxLength(500)]
        public string? LogoUrl { get; set; }
        [MaxLength(255)]
        public string? Website { get; set; }
        [MaxLength(100)]
        public string? SupportEmail { get; set; }

        // Audit
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        [MaxLength(100)]
        public string? CreatedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        [MaxLength(100)]
        public string? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedDate { get; set; }

        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    }

    // 2. Categories
    [Table("Categories")]
    public class Category
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        [Required]
        [MaxLength(150)]
        public string Slug { get; set; } = string.Empty;
        
        public int? ParentId { get; set; }
        public int Level { get; set; }
        
        [MaxLength(500)]
        public string? ImageUrl { get; set; }
        public int SortOrder { get; set; }
        public bool IsVisible { get; set; } = true;

        // Audit
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        [MaxLength(100)]
        public string? CreatedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        [MaxLength(100)]
        public string? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedDate { get; set; }

        [ForeignKey("ParentId")]
        public virtual Category? Parent { get; set; }
        public virtual ICollection<Category> Children { get; set; } = new List<Category>();
        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    }

    // 3. Products
    [Table("Products")]
    public class Product
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;
        [MaxLength(500)]
        public string? ShortDescription { get; set; }
        public string? Description { get; set; }

        public int ManufacturerId { get; set; }
        public int CategoryId { get; set; }

        public byte Status { get; set; } = 1;

        // Audit
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        [MaxLength(100)]
        public string? CreatedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        [MaxLength(100)]
        public string? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedDate { get; set; }

        [ForeignKey("ManufacturerId")]
        public virtual Manufacturer Manufacturer { get; set; } = null!;
        [ForeignKey("CategoryId")]
        public virtual Category Category { get; set; } = null!;
        public virtual ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    }

    // 4. ProductVariants
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
        public string? Specifications { get; set; }

        // Audit
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
        
        // Navigation properties to other modules can be added if needed (e.g., Inventory)
        public virtual ICollection<ProductSerial> Serials { get; set; } = new List<ProductSerial>();

        /// <summary>
        /// Số lượng tồn kho = đếm Serials có Status = 0 (Available).
        /// [NotMapped] — không tạo cột trong DB, chỉ dùng khi đã eager-load Serials.
        /// </summary>
        [NotMapped]
        public int StockQuantity => Serials?.Count(s => s.Status == 0) ?? 0;
    }


    // 6. ProductImages
    [Table("ProductImages")]
    public class ProductImage
    {
        [Key]
        public int Id { get; set; }
        public int VariantId { get; set; }

        [Required]
        [MaxLength(500)]
        public string ImageUrl { get; set; } = string.Empty;
        public bool IsMain { get; set; }
        public int SortOrder { get; set; }

        [ForeignKey("VariantId")]
        public virtual ProductVariant Variant { get; set; } = null!;
    }
}
