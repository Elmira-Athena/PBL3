using System;
using PBL3.Shared.DTOs.Common;

namespace PBL3.Shared.DTOs.Sale
{
    public class OrderSummaryResponse
    {
        public int Id { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string? CustomerAvatarUrl { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime CreatedDate { get; set; }
        public byte Status { get; set; }
        public byte PaymentStatus { get; set; }
    }
}
