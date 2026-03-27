using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PBL3.Shared.DTOs.Sale
{
    public class CreateOrderRequest
    {
        // --- Shipping Info ---
        [Required] public string ShipName { get; set; } = string.Empty;
        [Required] public string ShipPhone { get; set; } = string.Empty;
        [Required] public string ShipAddress { get; set; } = string.Empty;
        [Required] public string ShipCity { get; set; } = string.Empty;

        // --- Payment ---
        public byte PaymentMethod { get; set; } // 0: COD, 1: Banking, 2: VNPay
        public string? Note { get; set; }

        // --- Vouchers (Nhận DANH SÁCH mã giảm giá) ---
        /// <summary>
        /// Danh sách mã voucher (Code, không phải Id).
        /// Có thể rỗng nếu không áp dụng voucher.
        /// </summary>
        public List<string>? VoucherCodes { get; set; }

        // --- Cart Items ---
        public List<CreateOrderDetailRequest> Items { get; set; } = new();
    }

    public class CreateOrderDetailRequest
    {
        public int VariantId { get; set; }
        public int Quantity { get; set; }
    }
}
