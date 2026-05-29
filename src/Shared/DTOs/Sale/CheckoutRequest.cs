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
        /// Phí vận chuyển (tạm nhận từ Frontend hoặc fix cứng 30.000đ)
        /// </summary>
        public decimal ShippingFee { get; set; } = 30000;

        [MaxLength(500)]
        public string? Note { get; set; }

        // --- Buy Now Flow ---
        public bool IsBuyNow { get; set; }
        public int? BuyNowVariantId { get; set; }
        public int? BuyNowQuantity { get; set; }
    }
}
