using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
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

        public byte Status { get; set; } // 0: Pending, 1: Confirmed, 2: Shipping, 3: Success, 4: Cancelled, 5: Returned

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

        public decimal DiscountAmount { get; set; }

        public decimal TotalAmount { get; set; }

        public byte PaymentMethod { get; set; } // 0: COD, 1: Banking, 2: VNPay
        public byte PaymentStatus { get; set; } // 0: Unpaid, 1: Paid, 2: Refunded

        public byte OrderType { get; set; } // 0: Online, 1: POS

        [MaxLength(500)]
        public string? Note { get; set; }
        [MaxLength(255)]
        public string? CancelReason { get; set; }


        [ForeignKey("UserId")]
        public virtual AppUser? User { get; set; }
        // EmployeeId fk to AppUser

        /// <summary>
        /// Danh sách voucher đã áp dụng cho đơn hàng này.
        /// </summary>
        public virtual ICollection<VoucherUsage> VoucherUsages { get; set; } = new List<VoucherUsage>();

        public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
    }
}
