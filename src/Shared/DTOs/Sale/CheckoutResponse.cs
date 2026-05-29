using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PBL3.Shared.DTOs.Sale
{
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
