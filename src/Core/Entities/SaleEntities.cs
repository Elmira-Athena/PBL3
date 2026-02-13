using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    // 1. Vouchers
    [Table("Vouchers")]
    public class Voucher
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        public int DiscountType { get; set; } // 0: Amount, 1: Percentage
        public decimal DiscountValue { get; set; }

        public decimal MinOrderValue { get; set; }
        public decimal? MaxDiscountAmount { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public int Quantity { get; set; }
        public int UsedCount { get; set; }

        public bool IsActive { get; set; } = true;

        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
    }

    // 2. Orders
    [Table("Orders")]
    public class Order
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [MaxLength(20)]
        public string OrderCode { get; set; } = string.Empty;

        public Guid? UserId { get; set; }
        public Guid? EmployeeId { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        public int Status { get; set; } // 0: Pending, 1: Confirmed, 2: Shipping, 3: Success, 4: Cancelled, 5: Returned

        [Required]
        [MaxLength(100)]
        public string ShipName { get; set; } = string.Empty;
        [Required]
        [MaxLength(20)]
        public string ShipPhone { get; set; } = string.Empty;
        [Required]
        [MaxLength(255)]
        public string ShipAddress { get; set; } = string.Empty;
        [Required]
        [MaxLength(100)]
        public string ShipCity { get; set; } = string.Empty;

        public decimal SubTotal { get; set; }
        public decimal ShippingFee { get; set; }

        public int? VoucherId { get; set; }
        public decimal DiscountAmount { get; set; }

        public decimal TotalAmount { get; set; }

        public int PaymentMethod { get; set; } // 0: COD, 1: Banking, 2: VNPay
        public int PaymentStatus { get; set; } // 0: Unpaid, 1: Paid, 2: Refunded

        [MaxLength(500)]
        public string? Note { get; set; }
        [MaxLength(255)]
        public string? CancelReason { get; set; }


        [ForeignKey("UserId")]
        public virtual AppUser? User { get; set; }
        // EmployeeId fk to AppUser
        [ForeignKey("VoucherId")]
        public virtual Voucher? Voucher { get; set; }

        public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
    }

    // 3. OrderDetails
    [Table("OrderDetails")]
    public class OrderDetail
    {
        [Key]
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int VariantId { get; set; }

        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        
        // TotalLine is computed column
        public decimal TotalLine { get; set; }

        [ForeignKey("OrderId")]
        public virtual Order Order { get; set; } = null!;
        [ForeignKey("VariantId")]
        public virtual ProductVariant Variant { get; set; } = null!;
        
        public virtual ICollection<OrderSerial> OrderSerials { get; set; } = new List<OrderSerial>();
    }

    // 4. OrderSerials
    [Table("OrderSerials")]
    public class OrderSerial
    {
        [Key]
        public int Id { get; set; }
        public int OrderDetailId { get; set; }
        public int SerialId { get; set; }

        [ForeignKey("OrderDetailId")]
        public virtual OrderDetail OrderDetail { get; set; } = null!;
        [ForeignKey("SerialId")]
        public virtual ProductSerial Serial { get; set; } = null!;
    }

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
