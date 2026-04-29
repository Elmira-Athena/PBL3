using System.Net.Http.Json;
using PBL3.Shared.DTOs.Common;

namespace Client.Services.Cart
{
    public class CartClientService(IHttpClientFactory httpClientFactory) : ICartClientService
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

        public async Task<ApiResult<bool>> AddToCartAsync(int variantId, int quantity)
        {
            var client = _httpClientFactory.CreateClient("HushStoreAPI");
            var request = new { VariantId = variantId, Quantity = quantity };
            var response = await client.PostAsJsonAsync("/api/cart", request);
            
            if (response.IsSuccessStatusCode)
            {
                return new ApiResult<bool> { Success = true, Data = true };
            }

            return new ApiResult<bool> { Success = false, Message = "Failed to add to cart" };
        }
    }
}
