using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    // 1. Suppliers
    [Table("Suppliers")]
    public class Supplier
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        [MaxLength(100)]
        public string? ContactPerson { get; set; }
        [Required]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;
        [MaxLength(100)]
        public string? Email { get; set; }
        [MaxLength(255)]
        public string? Address { get; set; }
        [MaxLength(20)]
        public string? TaxCode { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }

        public virtual ICollection<ImportReceipt> ImportReceipts { get; set; } = new List<ImportReceipt>();
    }

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
        // Employee relation is implicit via Auth module

        public virtual ICollection<ImportReceiptDetail> Details { get; set; } = new List<ImportReceiptDetail>();
    }

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

        public byte Status { get; set; } // 0: Available, 1: Reserved, 2: Sold, 3: Defective, 4: Returned

        public int? OrderId { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? SoldDate { get; set; }

        [ForeignKey("VariantId")]
        public virtual ProductVariant Variant { get; set; } = null!;
        [ForeignKey("ImportReceiptId")]
        public virtual ImportReceipt ImportReceipt { get; set; } = null!;
        // OrderId relation link to Sale module
    }

    // 5. InventoryChecks
    [Table("InventoryChecks")]
    public class InventoryCheck
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [MaxLength(20)]
        public string CheckCode { get; set; } = string.Empty;
        public Guid EmployeeId { get; set; }
        public DateTime CheckDate { get; set; } = DateTime.UtcNow;

        [MaxLength(500)]
        public string? Note { get; set; }
        public byte Status { get; set; } // 0: Pending, 1: Completed

        public virtual ICollection<InventoryCheckDetail> Details { get; set; } = new List<InventoryCheckDetail>();
    }

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
        // Difference is computed column
        public int Difference { get; set; }

        [MaxLength(255)]
        public string? Reason { get; set; }

        [ForeignKey("CheckId")]
        public virtual InventoryCheck Check { get; set; } = null!;
        [ForeignKey("VariantId")]
        public virtual ProductVariant Variant { get; set; } = null!;
    }
}
