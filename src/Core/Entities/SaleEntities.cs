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

        public byte DiscountType { get; set; } // 0: Amount, 1: Percentage
        public decimal DiscountValue { get; set; }

        public decimal MinOrderValue { get; set; }
        public decimal? MaxDiscountAmount { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public int? Quantity { get; set; } // null = không giới hạn số lượng
        public int UsedCount { get; set; }

        public bool IsActive { get; set; } = true;

        // Quản lý nâng cao
        public int? MaxUsesPerUser { get; set; }        // null = không giới hạn lần dùng/người
        public byte ApplyFor { get; set; }              // 0: Both, 1: Online, 2: POS
        public bool IsStackable { get; set; }           // false = không được dùng chung voucher khác
        [MaxLength(500)]
        public string? Description { get; set; }

        // Soft delete & audit
        public DateTime CreatedDate { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedDate { get; set; }

        public virtual ICollection<VoucherUsage> VoucherUsages { get; set; } = new List<VoucherUsage>();
        public virtual ICollection<VoucherCategory> VoucherCategories { get; set; } = new List<VoucherCategory>();
    }

    // 1b. VoucherCategories (danh mục sản phẩm được áp dụng voucher)
    [Table("VoucherCategories")]
    public class VoucherCategory
    {
        public int VoucherId { get; set; }
        public int CategoryId { get; set; }

        [ForeignKey("VoucherId")]
        public virtual Voucher Voucher { get; set; } = null!;
        [ForeignKey("CategoryId")]
        public virtual Category Category { get; set; } = null!;
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

    // 6. VoucherUsages (Bảng trung gian: User đã dùng Voucher nào, trong Order nào)
    [Table("VoucherUsages")]
    public class VoucherUsage
    {
        [Key]
        public int Id { get; set; }

        public int VoucherId { get; set; }
        public Guid UserId { get; set; }
        public int OrderId { get; set; }

        /// <summary>
        /// Số tiền thực tế được giảm bởi voucher này trong đơn hàng.
        /// VD: Voucher giảm 20%, MaxDiscount = 50k, đơn 300k -> DiscountApplied = 50k.
        /// </summary>
        public decimal DiscountApplied { get; set; }

        public DateTime UsedDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Số thứ tự lần dùng của <b>chính khách này</b> với <b>chính voucher này</b>: 1, 2, 3…
        /// Cùng với unique index <c>UQ_VoucherUsages_UserId_VoucherId_SeqPerUser</c>, đây là thứ
        /// thực sự chặn việc dùng vượt <c>Voucher.MaxUsesPerUser</c>.
        /// </summary>
        /// <remarks>
        /// 🔴 <b>VÌ SAO CẦN CỘT NÀY, thay vì unique <c>(UserId, VoucherId)</c> cho gọn.</b>
        /// LoadProbe S03 đo được 1 khách dùng <b>10 lần</b> một mã có <c>MaxUsesPerUser = 1</c>,
        /// cả 10 request <c>200</c>: <c>GetUserVoucherUsageCountsAsync</c> rồi mới so sánh là
        /// check-then-act, cả 10 đều đọc thấy <c>0</c> trước khi ai kịp commit.
        ///
        /// Unique <c>(UserId, VoucherId)</c> chặn được S03, nhưng nó **cứng hoá** giả định
        /// "mỗi khách 1 lần". Validator chỉ yêu cầu <c>MaxUsesPerUser > 0</c>
        /// (<c>VoucherValidators</c>), tức app <b>cho phép</b> đặt 3 — và index đó sẽ chặn ngay
        /// lần dùng thứ hai, một hồi quy chỉ hiện ra khi có người thật dùng tính năng đó.
        /// Dữ liệu hiện toàn <c>NULL</c> là <em>ngẫu nhiên</em>, không phải hợp đồng.
        ///
        /// <b>Cách bất biến được giữ:</b> <c>SeqPerUser = (số lần đã dùng) + 1</c> tính bằng một
        /// câu <c>MAX</c>, và <b>unique index mới là thứ chốt</b> — hai request đồng thời cùng
        /// tính ra một số, một request đụng index và nhận <c>2601</c> → <c>409</c> qua
        /// <c>ConflictExceptionHandler</c>. Bấm lại thì đọc được số mới. Ở đây "bắt lỗi rồi thử
        /// lại" là công cụ ĐÚNG, khác hẳn chuyện sinh mã chứng từ: va chạm ở đây <em>hiếm</em>
        /// (một người bấm hai lần), còn ở đó va chạm là <em>chắc chắn</em> với mọi request.
        /// </remarks>
        public int SeqPerUser { get; set; } = 1;

        [ForeignKey("VoucherId")]
        public virtual Voucher Voucher { get; set; } = null!;
        [ForeignKey("UserId")]
        public virtual AppUser User { get; set; } = null!;
        [ForeignKey("OrderId")]
        public virtual Order Order { get; set; } = null!;
    }

    // 7. Warranties
    [Table("Warranties")]
    public class Warranty
    {
        [Key]
        public int Id { get; set; }
        public int SerialId { get; set; }
        public Guid? CustomerId { get; set; }
        public int OrderId { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public byte Status { get; set; } // 0: Active, 1: Expired, 2: Claimed

        [ForeignKey("SerialId")]
        public virtual ProductSerial Serial { get; set; } = null!;
        [ForeignKey("CustomerId")]
        public virtual AppUser? Customer { get; set; }
        [ForeignKey("OrderId")]
        public virtual Order Order { get; set; } = null!;
    }

    // 8. UserAddresses
    [Table("UserAddresses")]
    public class UserAddress
    {
        [Key]
        public int Id { get; set; }
        public Guid UserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string ReceiverName { get; set; } = string.Empty;
        [Required]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;
        [Required]
        [MaxLength(255)]
        public string AddressLine { get; set; } = string.Empty;
        [Required]
        [MaxLength(100)]
        public string City { get; set; } = string.Empty;

        public bool IsDefault { get; set; }

        [ForeignKey("UserId")]
        public virtual AppUser User { get; set; } = null!;
    }

    // 9. ProductReviews
    [Table("ProductReviews")]
    public class ProductReview
    {
        [Key]
        public int Id { get; set; }

        public int ProductId { get; set; }
        public Guid UserId { get; set; }

        public byte Rating { get; set; } // 1-5

        [MaxLength(200)]
        public string? Title { get; set; }
        [MaxLength(2000)]
        public string? Content { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public DateTime? DeletedDate { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; } = null!;
        [ForeignKey("UserId")]
        public virtual AppUser User { get; set; } = null!;
    }
}
