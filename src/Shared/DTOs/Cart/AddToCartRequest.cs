using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PBL3.Shared.DTOs.Cart
{
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
}
