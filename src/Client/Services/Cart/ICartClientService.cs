using PBL3.Shared.DTOs.Cart;
using PBL3.Shared.DTOs.Common;

namespace Client.Services.Cart
{
    public interface ICartClientService
    {
        Task<ApiResult<bool>> AddToCartAsync(int variantId, int quantity);
        Task<ApiResult<CartResponse>> GetMyCartAsync();
        Task<ApiResult<CartResponse>> UpdateQuantityAsync(int cartItemId, int quantity);
        Task<ApiResult<CartResponse>> RemoveItemAsync(int cartItemId);
    }
}
