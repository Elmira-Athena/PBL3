using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PBL3.Shared.DTOs.Cart
{
    // ===== REQUEST DTOs =====

    /// <summary>
    /// Request thêm sản phẩm vào giỏ hàng.
    /// </summary>
    public class AddToCartRequest
    {
        [Required(ErrorMessage = "Mã biến thể sản phẩm không được để trống.")]
        public int VariantId { get; set; }

        [Required(ErrorMessage = "Số lượng không được để trống.")]
        [Range(1, 999, ErrorMessage = "Số lượng phải từ 1 đến 999.")]
        public int Quantity { get; set; }
    }

    /// <summary>
    /// Request cập nhật số lượng một item trong giỏ.
    /// </summary>
    public class UpdateCartItemRequest
    {
        [Required(ErrorMessage = "Số lượng không được để trống.")]
        public int Quantity { get; set; }
    }

    // ===== RESPONSE DTOs =====

    /// <summary>
    /// Response chi tiết 1 item trong giỏ hàng.
    /// KHÔNG chứa StockQuantity hay CostPrice (bảo mật tồn kho).
    /// </summary>
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
    }

    /// <summary>
    /// Response toàn bộ giỏ hàng của User.
    /// </summary>
    public class CartResponse
    {
        public List<CartItemResponse> Items { get; set; } = new();
        public decimal TotalAmount { get; set; }
    }
}
