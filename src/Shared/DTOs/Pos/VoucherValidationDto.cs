using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System;

namespace PBL3.Shared.DTOs.Pos
{
    public class VoucherValidationDto
    {
        public bool IsValid { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public decimal DiscountAmount { get; set; } // Số tiền được giảm tương ứng với giỏ hàng
    }
}
