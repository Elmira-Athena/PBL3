using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Inventory;

namespace Client.Services.Inventory
{
    public class InventoryCheckClientService : IInventoryCheckClientService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<InventoryCheckClientService> _logger;
        private const string BaseUrl = "api/inventory-checks";

        public InventoryCheckClientService(HttpClient httpClient, ILogger<InventoryCheckClientService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
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
                _logger.LogError(ex, "Lỗi khi {Action}.", "tạo phiếu kiểm kê");
                return ApiResult<InventoryCheckDto>.Fail("Không tạo được phiếu kiểm kê. Vui lòng thử lại.");
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
                _logger.LogError(ex, "Lỗi khi {Action}.", "tải danh sách phiếu kiểm kê");
                return ApiResult<PagedResult<InventoryCheckListItemDto>>.Fail("Không tải được danh sách phiếu kiểm kê. Vui lòng thử lại.");
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
                _logger.LogError(ex, "Lỗi khi {Action}.", "tải chi tiết phiếu kiểm kê");
                return ApiResult<InventoryCheckDto>.Fail("Không tải được chi tiết phiếu kiểm kê. Vui lòng thử lại.");
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
                _logger.LogError(ex, "Lỗi khi {Action}.", "tải bảng theo dõi kiểm kê");
                return ApiResult<InventoryCheckDashboardDto>.Fail("Không tải được bảng theo dõi kiểm kê. Vui lòng thử lại.");
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
                _logger.LogError(ex, "Lỗi khi {Action}.", "tải danh sách serial của phiếu kiểm kê");
                return ApiResult<PagedResult<InventoryCheckSerialDto>>.Fail("Không tải được danh sách serial của phiếu kiểm kê. Vui lòng thử lại.");
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
                _logger.LogError(ex, "Lỗi khi {Action}.", "quét serial vào phiếu kiểm kê");
                return ApiResult<ScanResultDto>.Fail("Không ghi nhận được serial vừa quét. Vui lòng quét lại.");
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
                _logger.LogError(ex, "Lỗi khi {Action}.", "đánh dấu serial lỗi");
                return ApiResult<bool>.Fail("Không đánh dấu được serial lỗi. Vui lòng thử lại.");
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
                _logger.LogError(ex, "Lỗi khi {Action}.", "cập nhật lý do lệch kiểm kê");
                return ApiResult<bool>.Fail("Không cập nhật được lý do lệch. Vui lòng thử lại.");
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
                _logger.LogError(ex, "Lỗi khi {Action}.", "gửi duyệt phiếu kiểm kê");
                return ApiResult<bool>.Fail("Không gửi duyệt được phiếu kiểm kê. Vui lòng tải lại trang để xem trạng thái hiện tại rồi thử lại.");
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
                _logger.LogError(ex, "Lỗi khi {Action}.", "phê duyệt phiếu kiểm kê");
                return ApiResult<bool>.Fail("Không phê duyệt được phiếu kiểm kê. Vui lòng tải lại trang để xem trạng thái hiện tại rồi thử lại.");
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
                _logger.LogError(ex, "Lỗi khi {Action}.", "từ chối phiếu kiểm kê");
                return ApiResult<bool>.Fail("Không từ chối được phiếu kiểm kê. Vui lòng tải lại trang để xem trạng thái hiện tại rồi thử lại.");
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
                _logger.LogError(ex, "Lỗi khi {Action}.", "huỷ phiếu kiểm kê");
                return ApiResult<bool>.Fail("Không huỷ được phiếu kiểm kê. Vui lòng thử lại.");
            }
        }
    }
}
