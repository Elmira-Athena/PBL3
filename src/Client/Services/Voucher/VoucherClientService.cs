using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Vouchers;

namespace Client.Services.Voucher
{
    public class VoucherClientService : IVoucherClientService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<VoucherClientService> _logger;
        private const string BaseUrl = "api/vouchers";

        public VoucherClientService(HttpClient httpClient, ILogger<VoucherClientService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<ApiResult<PagedResult<VoucherDto>>> GetListAsync(VoucherFilterRequest request)
        {
            try
            {
                var queryParams = new List<string>();

                if (!string.IsNullOrWhiteSpace(request.Keyword))
                    queryParams.Add($"Keyword={Uri.EscapeDataString(request.Keyword)}");
                if (!string.IsNullOrWhiteSpace(request.StatusFilter))
                    queryParams.Add($"StatusFilter={Uri.EscapeDataString(request.StatusFilter)}");
                if (request.FromDate.HasValue)
                    queryParams.Add($"FromDate={Uri.EscapeDataString(request.FromDate.Value.ToString("o"))}");
                if (request.ToDate.HasValue)
                    queryParams.Add($"ToDate={Uri.EscapeDataString(request.ToDate.Value.ToString("o"))}");

                queryParams.Add($"PageNumber={request.PageNumber}");
                queryParams.Add($"PageSize={request.PageSize}");

                if (!string.IsNullOrWhiteSpace(request.SortBy))
                    queryParams.Add($"SortBy={Uri.EscapeDataString(request.SortBy)}");
                if (request.SortDescending)
                    queryParams.Add("SortDescending=true");

                var url = $"{BaseUrl}?{string.Join("&", queryParams)}";
                var result = await _httpClient.GetFromJsonAsync<ApiResult<PagedResult<VoucherDto>>>(url);
                return result ?? ApiResult<PagedResult<VoucherDto>>.Fail("Không thể tải danh sách voucher.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "tải danh sách mã giảm giá");
                return ApiResult<PagedResult<VoucherDto>>.Fail("Không tải được danh sách mã giảm giá. Vui lòng thử lại.");
            }
        }

        public async Task<ApiResult<VoucherDto>> GetByIdAsync(int id)
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<ApiResult<VoucherDto>>($"{BaseUrl}/{id}");
                return result ?? ApiResult<VoucherDto>.Fail("Không tìm thấy voucher.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "tải thông tin mã giảm giá");
                return ApiResult<VoucherDto>.Fail("Không tải được thông tin mã giảm giá. Vui lòng thử lại.");
            }
        }

        public async Task<ApiResult<VoucherDto>> CreateAsync(CreateVoucherRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(BaseUrl, request);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<VoucherDto>>();
                return result ?? ApiResult<VoucherDto>.Fail("Không thể tạo voucher.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "tạo mã giảm giá");
                return ApiResult<VoucherDto>.Fail("Không tạo được mã giảm giá. Vui lòng thử lại.");
            }
        }

        public async Task<ApiResult<VoucherDto>> UpdateAsync(int id, UpdateVoucherRequest request)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"{BaseUrl}/{id}", request);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<VoucherDto>>();
                return result ?? ApiResult<VoucherDto>.Fail("Không thể cập nhật voucher.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "cập nhật mã giảm giá");
                return ApiResult<VoucherDto>.Fail("Không cập nhật được mã giảm giá. Vui lòng thử lại.");
            }
        }

        public async Task<ApiResult<bool>> DeleteAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{BaseUrl}/{id}");
                var result = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();
                return result ?? ApiResult<bool>.Fail("Không thể xóa voucher.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "xoá mã giảm giá");
                return ApiResult<bool>.Fail("Không xoá được mã giảm giá. Vui lòng thử lại.");
            }
        }

        public async Task<ApiResult<VoucherDto>> ToggleStatusAsync(int id)
        {
            try
            {
                var response = await _httpClient.PatchAsync($"{BaseUrl}/{id}/toggle-status", null);
                if (!response.IsSuccessStatusCode)
                    return ApiResult<VoucherDto>.Fail($"Lỗi HTTP {(int)response.StatusCode}.");
                var result = await response.Content.ReadFromJsonAsync<ApiResult<VoucherDto>>();
                return result ?? ApiResult<VoucherDto>.Fail("Không thể thay đổi trạng thái voucher.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "bật/tắt mã giảm giá");
                return ApiResult<VoucherDto>.Fail("Không đổi được trạng thái mã giảm giá. Vui lòng thử lại.");
            }
        }

        public async Task<ApiResult<List<VoucherAvailabilityDto>>> GetAvailableForOrderAsync(
            GetAvailableVouchersRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/available-for-order", request);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<List<VoucherAvailabilityDto>>>();
                return result ?? ApiResult<List<VoucherAvailabilityDto>>.Fail("Không thể tải danh sách voucher.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "tải mã giảm giá áp dụng được cho đơn");
                return ApiResult<List<VoucherAvailabilityDto>>.Fail("Không tải được danh sách mã giảm giá. Vui lòng thử lại.");
            }
        }
    }
}
