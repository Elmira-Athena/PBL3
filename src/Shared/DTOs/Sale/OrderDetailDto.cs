using System;
using System.Collections.Generic;

namespace PBL3.Shared.DTOs.Sale
{
    /// <summary>
    /// DTO hiển thị chi tiết đơn hàng (kèm danh sách voucher đã dùng).
    /// </summary>
    public class OrderDetailDto
    {
        public int Id { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public byte Status { get; set; }

        // Shipping
        public string ShipName { get; set; } = string.Empty;
        public string ShipPhone { get; set; } = string.Empty;
        public string ShipAddress { get; set; } = string.Empty;
        public string ShipCity { get; set; } = string.Empty;

        // Order metadata
        public byte PaymentMethod { get; set; }
        public byte PaymentStatus { get; set; }
        public byte OrderType { get; set; }
        public string? Note { get; set; }
        public string? CancelReason { get; set; }

        // Money
        public decimal SubTotal { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }

        // Nested
        public List<OrderDetailLineDto> Items { get; set; } = new();
        public List<VoucherUsageDto> AppliedVouchers { get; set; } = new();
    }
}
