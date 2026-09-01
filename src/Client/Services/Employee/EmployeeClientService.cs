using Microsoft.Extensions.Logging;
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
        private readonly ILogger<EmployeeClientService> _logger;
        private const string BaseUrl = "api/employees";

        public EmployeeClientService(HttpClient httpClient, ILogger<EmployeeClientService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
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
                _logger.LogError(ex, "Lỗi khi {Action}.", "tải danh sách nhân viên");
                return ApiResult<PagedResult<EmployeeListDto>>.Fail("Không tải được danh sách nhân viên. Vui lòng thử lại.");
            }
        }

        public async Task<ApiResult<EmployeeListDto>> GetByIdAsync(Guid id)
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<ApiResult<EmployeeListDto>>($"{BaseUrl}/{id}");
                return result ?? ApiResult<EmployeeListDto>.Fail("Không tìm thấy nhân viên.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "tải thông tin nhân viên");
                return ApiResult<EmployeeListDto>.Fail("Không tải được thông tin nhân viên. Vui lòng thử lại.");
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
                _logger.LogError(ex, "Lỗi khi {Action}.", "tạo nhân viên");
                return ApiResult<EmployeeListDto>.Fail("Không tạo được nhân viên. Vui lòng thử lại.");
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
                _logger.LogError(ex, "Lỗi khi {Action}.", "cập nhật nhân viên");
                return ApiResult<EmployeeListDto>.Fail("Không cập nhật được nhân viên. Vui lòng thử lại.");
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
                _logger.LogError(ex, "Lỗi khi {Action}.", "khoá tài khoản nhân viên");
                return ApiResult<bool>.Fail("Không khoá được tài khoản nhân viên. Vui lòng thử lại.");
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
                _logger.LogError(ex, "Lỗi khi {Action}.", "mở khoá tài khoản nhân viên");
                return ApiResult<bool>.Fail("Không mở khoá được tài khoản nhân viên. Vui lòng thử lại.");
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
                _logger.LogError(ex, "Lỗi khi {Action}.", "tải danh sách kỹ thuật viên");
                return ApiResult<List<EmployeeDto>>.Fail("Không tải được danh sách kỹ thuật viên. Vui lòng thử lại.");
            }
        }
    }
}
