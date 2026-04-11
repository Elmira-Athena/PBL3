using System.Net.Http.Json;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Products;
using PBL3.Shared.DTOs.Suppliers;

namespace Client.Services.Supplier
{
    public class SupplierClientService : ISupplierClientService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "api/suppliers";

        public SupplierClientService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ApiResult<PagedResult<SupplierDto>>> GetListAsync(SupplierFilterRequest request)
        {
            try
            {
                var queryParams = new List<string>();

                if (!string.IsNullOrWhiteSpace(request.Keyword))
                    queryParams.Add($"Keyword={Uri.EscapeDataString(request.Keyword)}");

                queryParams.Add($"PageNumber={request.PageNumber}");
                queryParams.Add($"PageSize={request.PageSize}");

                if (!string.IsNullOrWhiteSpace(request.SortBy))
                    queryParams.Add($"SortBy={Uri.EscapeDataString(request.SortBy)}");
                if (request.SortDescending)
                    queryParams.Add("SortDescending=true");

                var url = $"{BaseUrl}?{string.Join("&", queryParams)}";

                var result = await _httpClient
                    .GetFromJsonAsync<ApiResult<PagedResult<SupplierDto>>>(url);
                return result ?? ApiResult<PagedResult<SupplierDto>>.Fail("Không thể tải danh sách nhà cung cấp.");
            }
            catch (Exception ex)
            {
                return ApiResult<PagedResult<SupplierDto>>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<SupplierDto>> GetByIdAsync(int id)
        {
            try
            {
                var result = await _httpClient
                    .GetFromJsonAsync<ApiResult<SupplierDto>>($"{BaseUrl}/{id}");
                return result ?? ApiResult<SupplierDto>.Fail("Không tìm thấy nhà cung cấp.");
            }
            catch (Exception ex)
            {
                return ApiResult<SupplierDto>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<SupplierDto>> CreateAsync(CreateSupplierRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(BaseUrl, request);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<SupplierDto>>();
                return result ?? ApiResult<SupplierDto>.Fail("Không thể tạo nhà cung cấp.");
            }
            catch (Exception ex)
            {
                return ApiResult<SupplierDto>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<SupplierDto>> UpdateAsync(int id, UpdateSupplierRequest request)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"{BaseUrl}/{id}", request);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<SupplierDto>>();
                return result ?? ApiResult<SupplierDto>.Fail("Không thể cập nhật nhà cung cấp.");
            }
            catch (Exception ex)
            {
                return ApiResult<SupplierDto>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<bool>> DeleteAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{BaseUrl}/{id}");
                var result = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();
                return result ?? ApiResult<bool>.Fail("Không thể xóa nhà cung cấp.");
            }
            catch (Exception ex)
            {
                return ApiResult<bool>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }
    }
}
