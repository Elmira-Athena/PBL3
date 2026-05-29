using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PBL3.Shared.DTOs.Cart
{
    /// <summary>
    /// Request cập nhật số lượng một item trong giỏ.
    /// </summary>
    public class UpdateCartItemRequest
    {
        [Required(ErrorMessage = "Số lượng không được để trống.")]
        public int Quantity { get; set; }
    }
}
