using System.Net.Http.Json;
using PBL3.Shared.DTOs.Cart;
using PBL3.Shared.DTOs.Common;

namespace Client.Services.Cart
{
    public class CartClientService(IHttpClientFactory httpClientFactory) : ICartClientService
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

        public async Task<ApiResult<bool>> AddToCartAsync(int variantId, int quantity)
        {
            var client = _httpClientFactory.CreateClient("HushStoreAPI");
            var request = new AddToCartRequest { VariantId = variantId, Quantity = quantity };
            var response = await client.PostAsJsonAsync("/api/cart", request);

            try
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResult<CartResponse>>();
                return new ApiResult<bool>
                {
                    Success = result?.Success ?? response.IsSuccessStatusCode,
                    Message = result?.Message,
                    Data    = response.IsSuccessStatusCode
                };
            }
            catch
            {
                return new ApiResult<bool>
                {
                    Success = false,
                    Message = "Không thể thêm vào giỏ hàng. Vui lòng thử lại."
                };
            }
        }

        public async Task<ApiResult<CartResponse>> GetMyCartAsync()
        {
            var client = _httpClientFactory.CreateClient("HushStoreAPI");
            var response = await client.GetAsync("/api/cart");
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ApiResult<CartResponse>>() 
                       ?? new ApiResult<CartResponse> { Success = false, Message = "Lỗi dữ liệu trả về" };
            }
            
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return new ApiResult<CartResponse> { Success = false, Message = "Unauthorized" };
            }

            return new ApiResult<CartResponse> { Success = false, Message = "Không thể lấy thông tin giỏ hàng" };
        }

        public async Task<ApiResult<CartResponse>> UpdateQuantityAsync(int cartItemId, int quantity)
        {
            var client = _httpClientFactory.CreateClient("HushStoreAPI");
            var request = new UpdateCartItemRequest { Quantity = quantity };
            var response = await client.PutAsJsonAsync($"/api/cart/items/{cartItemId}", request);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ApiResult<CartResponse>>() 
                       ?? new ApiResult<CartResponse> { Success = false, Message = "Lỗi dữ liệu trả về" };
            }
            
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return new ApiResult<CartResponse> { Success = false, Message = "Unauthorized" };
            }

            return new ApiResult<CartResponse> { Success = false, Message = "Không thể cập nhật số lượng" };
        }

        public async Task<ApiResult<CartResponse>> RemoveItemAsync(int cartItemId)
        {
            var client = _httpClientFactory.CreateClient("HushStoreAPI");
            var response = await client.DeleteAsync($"/api/cart/items/{cartItemId}");
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ApiResult<CartResponse>>() 
                       ?? new ApiResult<CartResponse> { Success = false, Message = "Lỗi dữ liệu trả về" };
            }
            
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return new ApiResult<CartResponse> { Success = false, Message = "Unauthorized" };
            }

            return new ApiResult<CartResponse> { Success = false, Message = "Không thể xóa sản phẩm khỏi giỏ hàng" };
        }
    }
}
