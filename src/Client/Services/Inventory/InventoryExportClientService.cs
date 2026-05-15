using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Inventory;

namespace Client.Services.Inventory
{
    public class InventoryExportClientService : IInventoryExportClientService
    {
        private readonly HttpClient _httpClient;

        public InventoryExportClientService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ApiResult<bool>> ExportOrderAsync(ExportOrderRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync("/api/inventory/export-order", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ApiResult<bool>>()
                       ?? ApiResult<bool>.Fail("Không nhận được dữ liệu hợp lệ từ server.");
            }

            try
            {
                var errorResult = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();
                return errorResult ?? ApiResult<bool>.Fail($"Lỗi HTTP: {response.StatusCode}");
            }
            catch
            {
                return ApiResult<bool>.Fail($"Lỗi HTTP: {response.StatusCode}");
            }
        }

        public async Task<ApiResult<bool>> ValidateSerialAsync(string serialNo, int variantId)
        {
            var url = $"/api/inventory/serials/validate?serialNo={Uri.EscapeDataString(serialNo)}&variantId={variantId}";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ApiResult<bool>>()
                       ?? ApiResult<bool>.Fail("Không nhận được dữ liệu hợp lệ từ server.");
            }

            try
            {
                var errorResult = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();
                return errorResult ?? ApiResult<bool>.Fail($"Lỗi HTTP: {response.StatusCode}");
            }
            catch
            {
                return ApiResult<bool>.Fail($"Lỗi HTTP: {response.StatusCode}");
            }
        }
    }
}
