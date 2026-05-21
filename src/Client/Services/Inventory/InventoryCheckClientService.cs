using System.Net.Http.Json;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Inventory;

namespace Client.Services.Inventory
{
    public class InventoryCheckClientService : IInventoryCheckClientService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "api/inventory-checks";

        public InventoryCheckClientService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ApiResult<InventoryCheckDto>> CreateAsync(CreateInventoryCheckRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(BaseUrl, request);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<InventoryCheckDto>>();
                return result ?? ApiResult<InventoryCheckDto>.Fail("Không thể tạo phiếu kiểm kê.");
            }
            catch (Exception ex)
            {
                return ApiResult<InventoryCheckDto>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<PagedResult<InventoryCheckListItemDto>>> GetListAsync(
            InventoryCheckFilterRequest filter, CancellationToken cancellationToken = default)
        {
            try
            {
                var q = new List<string>();
                if (!string.IsNullOrWhiteSpace(filter.Keyword))
                    q.Add($"Keyword={Uri.EscapeDataString(filter.Keyword)}");
                if (filter.Status.HasValue)
                    q.Add($"Status={filter.Status.Value}");
                if (filter.FromDate.HasValue)
                    q.Add($"FromDate={filter.FromDate.Value:yyyy-MM-dd}");
                if (filter.ToDate.HasValue)
                    q.Add($"ToDate={filter.ToDate.Value:yyyy-MM-dd}");
                if (filter.EmployeeId.HasValue)
                    q.Add($"EmployeeId={filter.EmployeeId.Value}");
                q.Add($"PageNumber={filter.PageNumber}");
                q.Add($"PageSize={filter.PageSize}");
                if (!string.IsNullOrWhiteSpace(filter.SortBy))
                    q.Add($"SortBy={Uri.EscapeDataString(filter.SortBy)}");
                if (filter.SortDescending)
                    q.Add("SortDescending=true");

                var url = $"{BaseUrl}?{string.Join("&", q)}";
                var result = await _httpClient.GetFromJsonAsync<ApiResult<PagedResult<InventoryCheckListItemDto>>>(url, cancellationToken);
                return result ?? ApiResult<PagedResult<InventoryCheckListItemDto>>.Fail("Không thể tải danh sách phiếu kiểm kê.");
            }
            catch (OperationCanceledException)
            {
                return ApiResult<PagedResult<InventoryCheckListItemDto>>.Fail(string.Empty);
            }
            catch (Exception ex)
            {
                return ApiResult<PagedResult<InventoryCheckListItemDto>>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<InventoryCheckDto>> GetByIdAsync(int id)
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<ApiResult<InventoryCheckDto>>($"{BaseUrl}/{id}");
                return result ?? ApiResult<InventoryCheckDto>.Fail("Không tìm thấy phiếu kiểm kê.");
            }
            catch (Exception ex)
            {
                return ApiResult<InventoryCheckDto>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<InventoryCheckDashboardDto>> GetDashboardAsync(int id)
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<ApiResult<InventoryCheckDashboardDto>>($"{BaseUrl}/{id}/dashboard");
                return result ?? ApiResult<InventoryCheckDashboardDto>.Fail("Không tải được dashboard.");
            }
            catch (Exception ex)
            {
                return ApiResult<InventoryCheckDashboardDto>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<PagedResult<InventoryCheckSerialDto>>> GetSerialsAsync(
            int checkId, InventoryCheckSerialFilterRequest filter, CancellationToken cancellationToken = default)
        {
            try
            {
                var q = new List<string>();
                if (filter.ScanStatus.HasValue)
                    q.Add($"ScanStatus={filter.ScanStatus.Value}");
                if (filter.VariantId.HasValue)
                    q.Add($"VariantId={filter.VariantId.Value}");
                q.Add($"PageNumber={filter.PageNumber}");
                q.Add($"PageSize={filter.PageSize}");

                var url = $"{BaseUrl}/{checkId}/serials?{string.Join("&", q)}";
                var result = await _httpClient.GetFromJsonAsync<ApiResult<PagedResult<InventoryCheckSerialDto>>>(url, cancellationToken);
                return result ?? ApiResult<PagedResult<InventoryCheckSerialDto>>.Fail("Không thể tải danh sách serial.");
            }
            catch (OperationCanceledException)
            {
                return ApiResult<PagedResult<InventoryCheckSerialDto>>.Fail(string.Empty);
            }
            catch (Exception ex)
            {
                return ApiResult<PagedResult<InventoryCheckSerialDto>>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<ScanResultDto>> ScanSerialAsync(int checkId, ScanSerialRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/{checkId}/scan", request);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<ScanResultDto>>();
                return result ?? ApiResult<ScanResultDto>.Fail("Không nhận được kết quả quét.");
            }
            catch (Exception ex)
            {
                return ApiResult<ScanResultDto>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<bool>> MarkDefectiveAsync(int checkId, int detailSerialId)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"{BaseUrl}/{checkId}/serials/{detailSerialId}/mark-defective", new { });
                var result = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();
                return result ?? ApiResult<bool>.Fail("Không thể đánh dấu hàng lỗi.");
            }
            catch (Exception ex)
            {
                return ApiResult<bool>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<bool>> UpdateReasonAsync(int checkId, int detailSerialId, UpdateScanReasonRequest request)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"{BaseUrl}/{checkId}/serials/{detailSerialId}/reason", request);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();
                return result ?? ApiResult<bool>.Fail("Không thể cập nhật lý do.");
            }
            catch (Exception ex)
            {
                return ApiResult<bool>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<bool>> SubmitAsync(int checkId)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/{checkId}/submit", new { });
                var result = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();
                return result ?? ApiResult<bool>.Fail("Không thể gửi duyệt phiếu.");
            }
            catch (Exception ex)
            {
                return ApiResult<bool>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<bool>> ApproveAsync(int checkId)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/{checkId}/approve", new { });
                var result = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();
                return result ?? ApiResult<bool>.Fail("Không thể phê duyệt phiếu.");
            }
            catch (Exception ex)
            {
                return ApiResult<bool>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<bool>> RejectAsync(int checkId, RejectInventoryCheckRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/{checkId}/reject", request);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();
                return result ?? ApiResult<bool>.Fail("Không thể từ chối phiếu.");
            }
            catch (Exception ex)
            {
                return ApiResult<bool>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task<ApiResult<bool>> CancelAsync(int checkId)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/{checkId}/cancel", new { });
                var result = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();
                return result ?? ApiResult<bool>.Fail("Không thể hủy phiếu.");
            }
            catch (Exception ex)
            {
                return ApiResult<bool>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }
    }
}
