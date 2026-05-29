using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PBL3.Shared.DTOs.Cart
{
    /// <summary>
    /// Response toàn bộ giỏ hàng của User.
    /// </summary>
    public class CartResponse
    {
        public List<CartItemResponse> Items { get; set; } = new();
        public decimal TotalAmount { get; set; }
    }
}
