using System.Net.Http.Json;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Employees;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Client.Services.Employee
{
    public class EmployeeClientService : IEmployeeClientService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "api/employees";

        public EmployeeClientService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ApiResult<PagedResult<EmployeeListDto>>> GetListAsync(EmployeeFilterRequest request)
        {
            try
            {
                var queryParams = new List<string>();

                if (!string.IsNullOrWhiteSpace(request.Keyword))
                    queryParams.Add($"keyword={Uri.EscapeDataString(request.Keyword)}");

                if (request.IsActive.HasValue)
                    queryParams.Add($"isActive={request.IsActive.Value.ToString().ToLower()}");

                if (request.Gender.HasValue)
                    queryParams.Add($"gender={request.Gender.Value}");

                queryParams.Add($"pageNumber={request.PageNumber}");
                queryParams.Add($"pageSize={request.PageSize}");

                if (!string.IsNullOrWhiteSpace(request.SortBy))
                    queryParams.Add($"sortBy={Uri.EscapeDataString(request.SortBy)}");

                if (request.SortDescending)
                    queryParams.Add("sortDescending=true");

                var url = $"{BaseUrl}?{string.Join("&", queryParams)}";
                var result = await _httpClient.GetFromJsonAsync<ApiResult<PagedResult<EmployeeListDto>>>(url);
                return result ?? ApiResult<PagedResult<EmployeeListDto>>.Fail("Không thể tải danh sách nhân viên.");
            }
            catch (Exception ex)
            {
                return ApiResult<PagedResult<EmployeeListDto>>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<EmployeeListDto>> CreateAsync(CreateEmployeeRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(BaseUrl, request);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<EmployeeListDto>>();
                return result ?? ApiResult<EmployeeListDto>.Fail("Không thể tạo nhân viên.");
            }
            catch (Exception ex)
            {
                return ApiResult<EmployeeListDto>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<EmployeeListDto>> UpdateAsync(Guid id, UpdateEmployeeRequest request)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"{BaseUrl}/{id}", request);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<EmployeeListDto>>();
                return result ?? ApiResult<EmployeeListDto>.Fail("Không thể cập nhật nhân viên.");
            }
            catch (Exception ex)
            {
                return ApiResult<EmployeeListDto>.Fail($"Lỗi kết nối: {ex.Message}");
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
                return result ?? ApiResult<bool>.Fail("Không thể khóa tài khoản nhân viên.");
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
                return result ?? ApiResult<bool>.Fail("Không thể mở khóa tài khoản nhân viên.");
            }
            catch (Exception ex)
            {
                return ApiResult<bool>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<List<EmployeeDto>>> GetTechniciansAsync()
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<ApiResult<List<EmployeeDto>>>($"{BaseUrl}/technicians");
                return result ?? ApiResult<List<EmployeeDto>>.Fail("Không thể tải danh sách kỹ thuật viên.");
            }
            catch (Exception ex)
            {
                return ApiResult<List<EmployeeDto>>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }
    }
}
