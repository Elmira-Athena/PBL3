using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PBL3.Shared.DTOs.Cart
{
    /// <summary>
    /// Response chi tiết 1 item trong giỏ hàng.
    public class CartItemResponse
    {
        public int Id { get; set; }
        public int VariantId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string VariantName { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal SubTotal { get; set; }
        public int StockQuantity { get; set; }
        public string ProductSlug { get; set; } = string.Empty;
    }
}
