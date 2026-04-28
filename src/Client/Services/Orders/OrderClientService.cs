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
    }
}
