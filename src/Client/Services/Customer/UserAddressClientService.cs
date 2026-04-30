using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Customers;

namespace Client.Services.Customer
{
    public class UserAddressClientService : IUserAddressClientService
    {
        private readonly HttpClient _httpClient;

        public UserAddressClientService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ApiResult<List<UserAddressDto>>> GetMyAddressesAsync()
        {
            var response = await _httpClient.GetAsync("api/storefront/user-addresses/my-addresses");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResult<List<UserAddressDto>>>();
                return result ?? ApiResult<List<UserAddressDto>>.Fail("Không thể parse dữ liệu.");
            }
            
            return ApiResult<List<UserAddressDto>>.Fail($"Lỗi gọi API: {response.ReasonPhrase}");
        }
    }
}
