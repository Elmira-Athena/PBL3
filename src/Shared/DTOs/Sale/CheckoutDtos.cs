using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PBL3.Shared.DTOs.Sale
{
    public class CheckoutRequest
    {
        [Required]
        public int UserAddressId { get; set; }

        public List<string>? VoucherCodes { get; set; }

        /// <summary>
        /// 0: COD, 1: Online (MoMo/VNPay)
        /// </summary>
        public byte PaymentMethod { get; set; }

        /// <summary>
        /// Phí vận chuyển cố định do SERVER áp. Không nhận từ client: trường cũ `ShippingFee` cho
        /// phép gửi số âm để tự giảm tổng tiền (pentest 2026-11-23 F3). Client chỉ đọc để hiển thị.
        /// </summary>
        public const decimal StandardShippingFee = 30000m;

        [MaxLength(500)]
        public string? Note { get; set; }

        // --- Buy Now Flow ---
        public bool IsBuyNow { get; set; }
        public int? BuyNowVariantId { get; set; }
        public int? BuyNowQuantity { get; set; }
    }

    public class CheckoutResponse
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public byte Status { get; set; }
        public byte PaymentMethod { get; set; }
        // URL redirect cho Online Payment (nullable — chỉ có khi PaymentMethod == 1)
        public string? PaymentUrl { get; set; }
    }
}
