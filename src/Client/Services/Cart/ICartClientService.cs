using PBL3.Shared.DTOs.Common;

namespace Client.Services.Cart
{
    public interface ICartClientService
    {
        Task<ApiResult<bool>> AddToCartAsync(int variantId, int quantity);
    }
}
