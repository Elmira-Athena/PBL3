using Microsoft.Extensions.Logging;
using System;
using System.Net.Http.Json;
using System.Threading.Tasks;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.ServiceTickets;

namespace Client.Services.ServiceTickets
{
    public class ServiceTicketClientService : IServiceTicketClientService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ServiceTicketClientService> _logger;

        public ServiceTicketClientService(HttpClient httpClient, ILogger<ServiceTicketClientService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<ApiResult<ServiceTicketIntakeEvaluationDto>> EvaluateIntakeAsync(string serialNumber)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/service-tickets/intake", serialNumber);
                return await response.Content.ReadFromJsonAsync<ApiResult<ServiceTicketIntakeEvaluationDto>>()
                    ?? new ApiResult<ServiceTicketIntakeEvaluationDto> { Message = "Lỗi đánh giá Serial." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "kiểm tra điều kiện tiếp nhận bảo hành");
                return new ApiResult<ServiceTicketIntakeEvaluationDto> { Message = "Không kiểm tra được điều kiện tiếp nhận. Vui lòng thử lại." };
            }
        }

        public async Task<ApiResult<ServiceTicketDetailDto>> CreateTicketAsync(CreateServiceTicketRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/service-tickets", request);
                return await response.Content.ReadFromJsonAsync<ApiResult<ServiceTicketDetailDto>>()
                    ?? new ApiResult<ServiceTicketDetailDto> { Message = "Lỗi tạo phiếu sửa chữa." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "tạo phiếu dịch vụ");
                return new ApiResult<ServiceTicketDetailDto> { Message = "Không tạo được phiếu dịch vụ. Vui lòng thử lại." };
            }
        }

        public async Task<ApiResult<PagedResult<ServiceTicketListDto>>> GetPagedListAsync(
            string? keyword, byte? status, DateTime? fromDate, DateTime? toDate,
            int pageNumber, int pageSize, string? sortBy, bool sortDescending)
        {
            try
            {
                var url = $"api/service-tickets?pageNumber={pageNumber}&pageSize={pageSize}";
                if (!string.IsNullOrEmpty(keyword))
                    url += $"&keyword={Uri.EscapeDataString(keyword)}";
                if (status.HasValue)
                    url += $"&status={status.Value}";
                if (fromDate.HasValue)
                    url += $"&fromDate={fromDate.Value:yyyy-MM-dd}";
                if (toDate.HasValue)
                    url += $"&toDate={toDate.Value:yyyy-MM-dd}";
                if (!string.IsNullOrEmpty(sortBy))
                    url += $"&sortBy={Uri.EscapeDataString(sortBy)}&sortDescending={sortDescending}";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return new ApiResult<PagedResult<ServiceTicketListDto>> { Message = "Lỗi tải danh sách phiếu." };

                try
                {
                    return await response.Content.ReadFromJsonAsync<ApiResult<PagedResult<ServiceTicketListDto>>>()
                        ?? new ApiResult<PagedResult<ServiceTicketListDto>> { Message = "Lỗi lấy danh sách." };
                }
                catch
                {
                    return new ApiResult<PagedResult<ServiceTicketListDto>> { Message = "Lỗi lấy danh sách." };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "tải danh sách phiếu dịch vụ");
                return new ApiResult<PagedResult<ServiceTicketListDto>> { Message = "Không tải được danh sách phiếu dịch vụ. Vui lòng thử lại." };
            }
        }

        public async Task<ApiResult<PagedResult<ServiceTicketListDto>>> GetMyTicketsAsync(
            string? keyword, byte? status, int pageNumber, int pageSize, string? sortBy, bool sortDescending)
        {
            try
            {
                var url = $"api/service-tickets/my?pageNumber={pageNumber}&pageSize={pageSize}";
                if (!string.IsNullOrEmpty(keyword))
                    url += $"&keyword={Uri.EscapeDataString(keyword)}";
                if (status.HasValue)
                    url += $"&status={status.Value}";
                if (!string.IsNullOrEmpty(sortBy))
                    url += $"&sortBy={Uri.EscapeDataString(sortBy)}&sortDescending={sortDescending}";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return new ApiResult<PagedResult<ServiceTicketListDto>> { Message = "Lỗi tải danh sách phiếu." };

                try
                {
                    return await response.Content.ReadFromJsonAsync<ApiResult<PagedResult<ServiceTicketListDto>>>()
                        ?? new ApiResult<PagedResult<ServiceTicketListDto>> { Message = "Lỗi lấy danh sách." };
                }
                catch
                {
                    return new ApiResult<PagedResult<ServiceTicketListDto>> { Message = "Lỗi lấy danh sách." };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "tải danh sách phiếu dịch vụ của tôi");
                return new ApiResult<PagedResult<ServiceTicketListDto>> { Message = "Không tải được danh sách phiếu dịch vụ. Vui lòng thử lại." };
            }
        }

        public async Task<ApiResult<ServiceTicketDetailDto>> GetByIdAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/service-tickets/{id}");
                if (!response.IsSuccessStatusCode)
                    return new ApiResult<ServiceTicketDetailDto> { Message = "Không thể tải phiếu." };

                try
                {
                    return await response.Content.ReadFromJsonAsync<ApiResult<ServiceTicketDetailDto>>()
                        ?? new ApiResult<ServiceTicketDetailDto> { Message = "Không tìm thấy phiếu." };
                }
                catch
                {
                    return new ApiResult<ServiceTicketDetailDto> { Message = "Không tìm thấy phiếu." };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "tải chi tiết phiếu dịch vụ");
                return new ApiResult<ServiceTicketDetailDto> { Message = "Không tải được chi tiết phiếu dịch vụ. Vui lòng thử lại." };
            }
        }

        public async Task<ApiResult<bool>> AssignTechnicianAsync(int ticketId, AssignTechnicianRequest request)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"api/service-tickets/{ticketId}/assign", request);
                return await response.Content.ReadFromJsonAsync<ApiResult<bool>>()
                    ?? new ApiResult<bool> { Message = "Lỗi gán kỹ thuật viên." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "phân công kỹ thuật viên");
                return new ApiResult<bool> { Message = "Không phân công được kỹ thuật viên. Vui lòng thử lại." };
            }
        }

        public async Task<ApiResult<bool>> RecordDiagnosisAsync(int ticketId, RecordDiagnosisRequest request)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"api/service-tickets/{ticketId}/diagnosis", request);
                return await response.Content.ReadFromJsonAsync<ApiResult<bool>>()
                    ?? new ApiResult<bool> { Message = "Lỗi ghi kết luận chẩn đoán." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "ghi kết quả chẩn đoán");
                return new ApiResult<bool> { Message = "Không lưu được kết quả chẩn đoán. Vui lòng thử lại." };
            }
        }

        public async Task<ApiResult<bool>> ChooseBranchAsync(int ticketId, ChooseBranchRequest request)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"api/service-tickets/{ticketId}/branch", request);
                return await response.Content.ReadFromJsonAsync<ApiResult<bool>>()
                    ?? new ApiResult<bool> { Message = "Lỗi chọn loại sửa chữa." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "chọn hướng xử lý phiếu dịch vụ");
                return new ApiResult<bool> { Message = "Không lưu được hướng xử lý. Vui lòng thử lại." };
            }
        }

        public async Task<ApiResult<QuotationDto>> CreateQuotationAsync(int ticketId, CreateQuotationRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"api/service-tickets/{ticketId}/quotation", request);
                return await response.Content.ReadFromJsonAsync<ApiResult<QuotationDto>>()
                    ?? new ApiResult<QuotationDto> { Message = "Lỗi tạo báo giá." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "tạo báo giá");
                return new ApiResult<QuotationDto> { Message = "Không tạo được báo giá. Vui lòng thử lại." };
            }
        }

        public async Task<ApiResult<bool>> AcceptQuotationAsync(int ticketId, int quotationId, AcceptQuotationRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"api/service-tickets/{ticketId}/quotation/{quotationId}/accept", request);
                return await response.Content.ReadFromJsonAsync<ApiResult<bool>>()
                    ?? new ApiResult<bool> { Message = "Lỗi duyệt báo giá." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "duyệt báo giá");
                return new ApiResult<bool> { Message = "Không duyệt được báo giá. Vui lòng tải lại trang để xem trạng thái báo giá rồi thử lại." };
            }
        }

        public async Task<ApiResult<bool>> RejectQuotationAsync(int ticketId, int quotationId, RejectQuotationRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"api/service-tickets/{ticketId}/quotation/{quotationId}/reject", request);
                return await response.Content.ReadFromJsonAsync<ApiResult<bool>>()
                    ?? new ApiResult<bool> { Message = "Lỗi từ chối báo giá." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "từ chối báo giá");
                return new ApiResult<bool> { Message = "Không từ chối được báo giá. Vui lòng tải lại trang để xem trạng thái báo giá rồi thử lại." };
            }
        }

        public async Task<ApiResult<RmaShipmentDto>> CreateRmaShipmentAsync(int ticketId, CreateRmaShipmentRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"api/service-tickets/{ticketId}/rma", request);
                return await response.Content.ReadFromJsonAsync<ApiResult<RmaShipmentDto>>()
                    ?? new ApiResult<RmaShipmentDto> { Message = "Lỗi tạo phiếu gửi hãng." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "tạo lô gửi hãng");
                return new ApiResult<RmaShipmentDto> { Message = "Không tạo được lô gửi hãng. Vui lòng thử lại." };
            }
        }

        public async Task<ApiResult<bool>> RecordRmaResolutionAsync(int ticketId, UpdateRmaResolutionRequest request)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"api/service-tickets/{ticketId}/rma/resolution", request);
                return await response.Content.ReadFromJsonAsync<ApiResult<bool>>()
                    ?? new ApiResult<bool> { Message = "Lỗi cập nhật kết quả RMA." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "ghi kết quả xử lý từ hãng");
                return new ApiResult<bool> { Message = "Không lưu được kết quả xử lý từ hãng. Vui lòng thử lại." };
            }
        }

        public async Task<ApiResult<bool>> Perform1For1SwapAsync(int ticketId, Perform1For1SwapRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"api/service-tickets/{ticketId}/swap", request);
                return await response.Content.ReadFromJsonAsync<ApiResult<bool>>()
                    ?? new ApiResult<bool> { Message = "Lỗi thực hiện đổi 1-1." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "đổi máy 1-1");
                return new ApiResult<bool> { Message = "Không thực hiện được đổi máy 1-1. Vui lòng tải lại trang để xem trạng thái phiếu rồi thử lại." };
            }
        }

        public async Task<ApiResult<bool>> StartRepairAsync(int ticketId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"api/service-tickets/{ticketId}/start-repair", null);
                if (!response.IsSuccessStatusCode)
                    return new ApiResult<bool> { Message = $"Lỗi HTTP {(int)response.StatusCode}." };
                return await response.Content.ReadFromJsonAsync<ApiResult<bool>>()
                    ?? new ApiResult<bool> { Message = "Lỗi bắt đầu sửa chữa." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "bắt đầu sửa chữa");
                return new ApiResult<bool> { Message = "Không chuyển được phiếu sang trạng thái đang sửa. Vui lòng thử lại." };
            }
        }

        public async Task<ApiResult<bool>> MarkWaitingPartsAsync(int ticketId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"api/service-tickets/{ticketId}/waiting-parts", null);
                if (!response.IsSuccessStatusCode)
                    return new ApiResult<bool> { Message = $"Lỗi HTTP {(int)response.StatusCode}." };
                return await response.Content.ReadFromJsonAsync<ApiResult<bool>>()
                    ?? new ApiResult<bool> { Message = "Lỗi ghi nhận chờ phụ tùng." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "đánh dấu chờ linh kiện");
                return new ApiResult<bool> { Message = "Không đánh dấu được chờ linh kiện. Vui lòng thử lại." };
            }
        }

        public async Task<ApiResult<bool>> ResumeRepairAsync(int ticketId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"api/service-tickets/{ticketId}/resume-repair", null);
                if (!response.IsSuccessStatusCode)
                    return new ApiResult<bool> { Message = $"Lỗi HTTP {(int)response.StatusCode}." };
                return await response.Content.ReadFromJsonAsync<ApiResult<bool>>()
                    ?? new ApiResult<bool> { Message = "Lỗi tiếp tục sửa chữa." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "tiếp tục sửa chữa");
                return new ApiResult<bool> { Message = "Không tiếp tục được việc sửa chữa. Vui lòng thử lại." };
            }
        }

        public async Task<ApiResult<bool>> CompleteRepairAsync(int ticketId, CompleteRepairRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"api/service-tickets/{ticketId}/complete", request);
                return await response.Content.ReadFromJsonAsync<ApiResult<bool>>()
                    ?? new ApiResult<bool> { Message = "Lỗi hoàn tất sửa chữa." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "hoàn tất sửa chữa");
                return new ApiResult<bool> { Message = "Không hoàn tất được sửa chữa. Vui lòng thử lại." };
            }
        }

        public async Task<ApiResult<ServiceInvoiceDetailDto>> IssueServiceInvoiceAsync(int ticketId, IssueServiceInvoiceRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"api/service-tickets/{ticketId}/invoice", request);
                return await response.Content.ReadFromJsonAsync<ApiResult<ServiceInvoiceDetailDto>>()
                    ?? new ApiResult<ServiceInvoiceDetailDto> { Message = "Lỗi phát hành hóa đơn dịch vụ." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "phát hành hoá đơn dịch vụ");
                return new ApiResult<ServiceInvoiceDetailDto> { Message = "Không phát hành được hoá đơn dịch vụ. Vui lòng tải lại trang để kiểm tra phiếu trước khi thử lại." };
            }
        }

        public async Task<ApiResult<bool>> CancelTicketAsync(int ticketId, CancelTicketRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"api/service-tickets/{ticketId}/cancel", request);
                return await response.Content.ReadFromJsonAsync<ApiResult<bool>>()
                    ?? new ApiResult<bool> { Message = "Lỗi hủy phiếu." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "huỷ phiếu dịch vụ");
                return new ApiResult<bool> { Message = "Không huỷ được phiếu dịch vụ. Vui lòng thử lại." };
            }
        }

        public async Task<ApiResult<List<ServiceTicketStatusHistoryDto>>> GetHistoryAsync(int ticketId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/service-tickets/{ticketId}/history");
                return await response.Content.ReadFromJsonAsync<ApiResult<List<ServiceTicketStatusHistoryDto>>>()
                    ?? new ApiResult<List<ServiceTicketStatusHistoryDto>> { Message = "Lỗi lấy lịch sử." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "tải lịch sử trạng thái phiếu dịch vụ");
                return new ApiResult<List<ServiceTicketStatusHistoryDto>> { Message = "Không tải được lịch sử phiếu dịch vụ. Vui lòng thử lại." };
            }
        }

        public async Task<ApiResult<List<SerialRepairLogDto>>> GetSerialRepairHistoryAsync(string serialNumber)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/service-tickets/serials/{Uri.EscapeDataString(serialNumber)}/repair-history");
                return await response.Content.ReadFromJsonAsync<ApiResult<List<SerialRepairLogDto>>>()
                    ?? new ApiResult<List<SerialRepairLogDto>> { Message = "Lỗi lấy lịch sử sửa chữa." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi {Action}.", "tải lịch sử sửa chữa của serial");
                return new ApiResult<List<SerialRepairLogDto>> { Message = "Không tải được lịch sử sửa chữa của serial. Vui lòng thử lại." };
            }
        }
    }
}
