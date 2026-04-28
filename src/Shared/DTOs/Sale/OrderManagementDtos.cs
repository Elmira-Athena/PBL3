using System;
using PBL3.Shared.DTOs.Common;

namespace PBL3.Shared.DTOs.Sale
{
    public class OrderFilterRequest
    {
        public string? Keyword { get; set; }
        public int? Status { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class OrderSummaryResponse
    {
        public int Id { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public DateTime CreatedDate { get; set; }
        public byte Status { get; set; }
        public byte PaymentStatus { get; set; }
    }

    public class CancelOrderRequest
    {
        public string CancelReason { get; set; } = string.Empty;
    }
}
