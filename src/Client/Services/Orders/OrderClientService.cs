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
            try
            {
                var response = await _httpClient.GetAsync($"/api/orders/{id}");
                if (!response.IsSuccessStatusCode)
                    return ApiResult<OrderDetailDto>.Fail($"Lỗi HTTP {(int)response.StatusCode}.");
                var result = await response.Content.ReadFromJsonAsync<ApiResult<OrderDetailDto>>();
                return result ?? ApiResult<OrderDetailDto>.Fail("Không nhận được phản hồi từ máy chủ");
            }
            catch (Exception ex)
            {
                return ApiResult<OrderDetailDto>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<PagedResult<OrderSummaryResponse>> GetPagedOrdersAsync(OrderFilterRequest request)
        {
            try
            {
                var queryString = $"?pageIndex={request.PageIndex}&pageSize={request.PageSize}";
                if (!string.IsNullOrEmpty(request.Keyword))
                    queryString += $"&keyword={request.Keyword}";
                if (request.Status.HasValue)
                    queryString += $"&status={request.Status.Value}";
                if (request.MinStatus.HasValue)
                    queryString += $"&minStatus={request.MinStatus.Value}";
                if (request.MaxStatus.HasValue)
                    queryString += $"&maxStatus={request.MaxStatus.Value}";
                if (request.FromDate.HasValue)
                    queryString += $"&fromDate={request.FromDate.Value:yyyy-MM-ddTHH:mm:ss}";
                if (request.ToDate.HasValue)
                    queryString += $"&toDate={request.ToDate.Value:yyyy-MM-ddTHH:mm:ss}";

                var response = await _httpClient.GetAsync($"/api/orders{queryString}");
                if (!response.IsSuccessStatusCode)
                    return new PagedResult<OrderSummaryResponse>();
                var result = await response.Content.ReadFromJsonAsync<ApiResult<PagedResult<OrderSummaryResponse>>>();
                return result?.Data ?? new PagedResult<OrderSummaryResponse>();
            }
            catch
            {
                return new PagedResult<OrderSummaryResponse>();
            }
        }

        public async Task<ApiResult<PagedResult<OrderSummaryResponse>>> GetMyOrdersAsync(OrderFilterRequest request)
        {
            try
            {
                var queryString = $"?pageIndex={request.PageIndex}&pageSize={request.PageSize}";
                if (request.Status.HasValue)
                    queryString += $"&status={request.Status.Value}";

                var response = await _httpClient.GetAsync($"/api/orders/my{queryString}");
                if (!response.IsSuccessStatusCode)
                    return ApiResult<PagedResult<OrderSummaryResponse>>.Fail($"Lỗi HTTP {(int)response.StatusCode}.");
                var result = await response.Content.ReadFromJsonAsync<ApiResult<PagedResult<OrderSummaryResponse>>>();
                return result ?? ApiResult<PagedResult<OrderSummaryResponse>>.Fail("Không nhận được phản hồi từ máy chủ");
            }
            catch (Exception ex)
            {
                return ApiResult<PagedResult<OrderSummaryResponse>>.Fail($"Lỗi kết nối: {ex.Message}");
            }
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

        public async Task<ApiResult<CheckoutResponse>> CheckoutAsync(CheckoutRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/orders/checkout", request);
                if (!response.IsSuccessStatusCode)
                    return ApiResult<CheckoutResponse>.Fail($"Lỗi HTTP {(int)response.StatusCode}.");
                var result = await response.Content.ReadFromJsonAsync<ApiResult<CheckoutResponse>>();
                return result ?? ApiResult<CheckoutResponse>.Fail("Không nhận được phản hồi từ máy chủ");
            }
            catch (Exception ex)
            {
                return ApiResult<CheckoutResponse>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<bool>> CompleteOrderAsync(int id)
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/orders/{id}/complete", new { });

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();
                return result ?? ApiResult<bool>.Fail("Không nhận được phản hồi từ máy chủ");
            }

            return ApiResult<bool>.Fail($"Lỗi HTTP: {response.StatusCode}");
        }

        public async Task<ApiResult<bool>> ConfirmOrderAsync(int id)
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/orders/{id}/confirm", new { });

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();
                return result ?? ApiResult<bool>.Fail("Không nhận được phản hồi từ máy chủ");
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

        public async Task<ApiResult<bool>> CancelMyOrderAsync(int id, string cancelReason)
        {
            try
            {
                var request = new CancelOrderRequest { CancelReason = cancelReason };
                var response = await _httpClient.PutAsJsonAsync($"/api/orders/my/{id}/cancel", request);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();
                return result ?? ApiResult<bool>.Fail("Không nhận được phản hồi từ máy chủ");
            }
            catch (Exception ex)
            {
                return ApiResult<bool>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<bool>> ConfirmReceivedAsync(int id)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"/api/orders/my/{id}/confirm-received", new { });
                var result = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();
                return result ?? ApiResult<bool>.Fail("Không nhận được phản hồi từ máy chủ");
            }
            catch (Exception ex)
            {
                return ApiResult<bool>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }
    }
}
