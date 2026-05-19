using System.Net.Http.Json;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Customers;
using PBL3.Shared.DTOs.Products;

namespace Client.Services.Customer
{
    public class CustomerClientService : ICustomerClientService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "api/customers";

        public CustomerClientService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ApiResult<PagedResult<CustomerDto>>> GetListAsync(CustomerFilterRequest request)
        {
            try
            {
                var queryParams = new List<string>();

                if (!string.IsNullOrWhiteSpace(request.Keyword))
                    queryParams.Add($"Keyword={Uri.EscapeDataString(request.Keyword)}");

                if (request.IsActive.HasValue)
                    queryParams.Add($"IsActive={request.IsActive.Value.ToString().ToLower()}");

                if (request.Gender.HasValue)
                    queryParams.Add($"Gender={request.Gender.Value}");

                queryParams.Add($"PageNumber={request.PageNumber}");
                queryParams.Add($"PageSize={request.PageSize}");

                if (!string.IsNullOrWhiteSpace(request.SortBy))
                    queryParams.Add($"SortBy={Uri.EscapeDataString(request.SortBy)}");
                if (request.SortDescending)
                    queryParams.Add("SortDescending=true");

                var url = $"{BaseUrl}?{string.Join("&", queryParams)}";

                var result = await _httpClient.GetFromJsonAsync<ApiResult<PagedResult<CustomerDto>>>(url);
                return result ?? ApiResult<PagedResult<CustomerDto>>.Fail("Không thể tải danh sách khách hàng.");
            }
            catch (Exception ex)
            {
                return ApiResult<PagedResult<CustomerDto>>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<CustomerDetailDto>> GetByIdAsync(Guid id)
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<ApiResult<CustomerDetailDto>>($"{BaseUrl}/{id}");
                return result ?? ApiResult<CustomerDetailDto>.Fail("Không tìm thấy khách hàng.");
            }
            catch (Exception ex)
            {
                return ApiResult<CustomerDetailDto>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<CustomerDto>> CreateAsync(CreateCustomerRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(BaseUrl, request);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<CustomerDto>>();
                return result ?? ApiResult<CustomerDto>.Fail("Không thể tạo khách hàng.");
            }
            catch (Exception ex)
            {
                return ApiResult<CustomerDto>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<CustomerDto>> UpdateAsync(Guid id, UpdateCustomerRequest request)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"{BaseUrl}/{id}", request);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<CustomerDto>>();
                return result ?? ApiResult<CustomerDto>.Fail("Không thể cập nhật khách hàng.");
            }
            catch (Exception ex)
            {
                return ApiResult<CustomerDto>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<bool>> DeactivateAsync(Guid id, string? lockReason = null)
        {
            try
            {
                var url = string.IsNullOrEmpty(lockReason)
                    ? $"{BaseUrl}/{id}"
                    : $"{BaseUrl}/{id}?lockReason={Uri.EscapeDataString(lockReason)}";
                var response = await _httpClient.DeleteAsync(url);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();
                return result ?? ApiResult<bool>.Fail("Không thể khóa tài khoản khách hàng.");
            }
            catch (Exception ex)
            {
                return ApiResult<bool>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<bool>> ReactivateAsync(Guid id)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"{BaseUrl}/{id}/activate", (object?)null);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();
                return result ?? ApiResult<bool>.Fail("Không thể mở khóa tài khoản khách hàng.");
            }
            catch (Exception ex)
            {
                return ApiResult<bool>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<CustomerDto>> GetMyProfileAsync()
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<ApiResult<CustomerDto>>("api/storefront/profile/me");
                return result ?? ApiResult<CustomerDto>.Fail("Không thể tải thông tin cá nhân.");
            }
            catch (Exception ex)
            {
                return ApiResult<CustomerDto>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<CustomerDto>> UpdateMyProfileAsync(UpdateCustomerRequest request)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync("api/storefront/profile/me", request);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<CustomerDto>>();
                return result ?? ApiResult<CustomerDto>.Fail("Không thể cập nhật thông tin cá nhân.");
            }
            catch (Exception ex)
            {
                return ApiResult<CustomerDto>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }
    }
}
