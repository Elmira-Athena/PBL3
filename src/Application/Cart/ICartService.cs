using System;
using System.Threading.Tasks;
using PBL3.Shared.DTOs.Cart;
using PBL3.Shared.DTOs.Common;

namespace PBL3.Application.Cart
{
    public interface ICartService
    {
        /// <summary>
        /// Lấy toàn bộ giỏ hàng của User.
        /// </summary>
        Task<ApiResult<CartResponse>> GetMyCartAsync(Guid userId);

        /// <summary>
        /// Thêm sản phẩm vào giỏ. Nếu Variant đã tồn tại → cộng dồn Quantity.
        /// </summary>
        Task<ApiResult<CartResponse>> AddToCartAsync(Guid userId, AddToCartRequest request);

        /// <summary>
        /// Cập nhật số lượng một item. Nếu Quantity &lt;= 0 → tự động xóa item.
        /// </summary>
        Task<ApiResult<CartResponse>> UpdateQuantityAsync(Guid userId, int cartItemId, UpdateCartItemRequest request);

        /// <summary>
        /// Xóa 1 item khỏi giỏ.
        /// </summary>
        Task<ApiResult<CartResponse>> RemoveItemAsync(Guid userId, int cartItemId);

        /// <summary>
        /// Xóa toàn bộ giỏ hàng (dùng sau Checkout thành công).
        /// </summary>
        Task<ApiResult<bool>> ClearCartAsync(Guid userId);
    }
}
