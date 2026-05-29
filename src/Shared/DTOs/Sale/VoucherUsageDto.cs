using System;
using System.Collections.Generic;

namespace PBL3.Shared.DTOs.Sale
{
    /// <summary>
    /// Thông tin voucher đã áp dụng (hiển thị trong chi tiết đơn).
    /// </summary>
    public class VoucherUsageDto
    {
        public string VoucherCode { get; set; } = string.Empty;
        public string VoucherName { get; set; } = string.Empty;
        public byte DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public decimal DiscountApplied { get; set; } // Số tiền thực tế được giảm
    }
}
