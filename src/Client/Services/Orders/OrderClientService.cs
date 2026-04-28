using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Sale;

namespace Client.Services.Orders
{
    public class OrderClientService : IOrderClientService
    {
        private readonly HttpClient _httpClient;

        public OrderClientService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ApiResult<OrderDetailDto>> GetByIdAsync(int id)
        {
            return await _httpClient.GetFromJsonAsync<ApiResult<OrderDetailDto>>($"/api/orders/{id}")
                   ?? ApiResult<OrderDetailDto>.Fail("Không nhận được phản hồi từ máy chủ");
        }

        public async Task<PagedResult<OrderSummaryResponse>> GetPagedOrdersAsync(OrderFilterRequest request)
        {
            var queryString = $"?pageIndex={request.PageIndex}&pageSize={request.PageSize}";
            if (!string.IsNullOrEmpty(request.Keyword))
                queryString += $"&keyword={request.Keyword}";
            if (request.Status.HasValue)
                queryString += $"&status={request.Status.Value}";
            if (request.FromDate.HasValue)
                queryString += $"&fromDate={request.FromDate.Value:yyyy-MM-ddTHH:mm:ss}";
            if (request.ToDate.HasValue)
                queryString += $"&toDate={request.ToDate.Value:yyyy-MM-ddTHH:mm:ss}";

            return await _httpClient.GetFromJsonAsync<PagedResult<OrderSummaryResponse>>($"/api/orders{queryString}")
                   ?? new PagedResult<OrderSummaryResponse>();
        }

        public async Task<ApiResult<bool>> CancelOrderAsync(int id, string cancelReason)
        {
            var request = new CancelOrderRequest { CancelReason = cancelReason };
            var response = await _httpClient.PutAsJsonAsync($"/api/orders/{id}/cancel", request);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();
                return result ?? ApiResult<bool>.Fail("Không nhận được phản hồi từ máy chủ");
            }
            
            return ApiResult<bool>.Fail($"Lỗi HTTP: {response.StatusCode}");
        }
    }
}
