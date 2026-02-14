using System.Net.Http.Json;
using PBL3.Shared.DTOs.Common;

namespace Client.Services.Inventory
{
    public class ProductSerialClientService : IProductSerialClientService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "api/product-serials";

        public ProductSerialClientService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ApiResult<bool>> CheckExistAsync(string serialNumber, int variantId)
        {
            try
            {
                var url = $"{BaseUrl}/check-exist?serialNumber={Uri.EscapeDataString(serialNumber)}&variantId={variantId}";
                var result = await _httpClient.GetFromJsonAsync<ApiResult<bool>>(url);
                return result ?? ApiResult<bool>.Fail("Không thể kiểm tra mã Serial.");
            }
            catch (Exception ex)
            {
                return ApiResult<bool>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }
    }
}
