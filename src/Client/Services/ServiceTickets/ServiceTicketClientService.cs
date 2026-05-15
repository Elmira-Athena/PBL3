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

        public ServiceTicketClientService(HttpClient httpClient)
        {
            _httpClient = httpClient;
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
                return new ApiResult<ServiceTicketIntakeEvaluationDto> { Message = $"Lỗi: {ex.Message}" };
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
                return new ApiResult<ServiceTicketDetailDto> { Message = $"Lỗi: {ex.Message}" };
            }
        }

        public async Task<ApiResult<PagedResult<ServiceTicketListDto>>> GetPagedListAsync(
            string? keyword, int pageNumber, int pageSize, string? sortBy, bool sortDescending)
        {
            try
            {
                var url = $"api/service-tickets?pageNumber={pageNumber}&pageSize={pageSize}";
                if (!string.IsNullOrEmpty(keyword))
                    url += $"&keyword={Uri.EscapeDataString(keyword)}";
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
                return new ApiResult<PagedResult<ServiceTicketListDto>> { Message = $"Lỗi: {ex.Message}" };
            }
        }

        public async Task<ApiResult<PagedResult<ServiceTicketListDto>>> GetMyTicketsAsync(
            string? keyword, int pageNumber, int pageSize, string? sortBy, bool sortDescending)
        {
            try
            {
                var url = $"api/service-tickets/my?pageNumber={pageNumber}&pageSize={pageSize}";
                if (!string.IsNullOrEmpty(keyword))
                    url += $"&keyword={Uri.EscapeDataString(keyword)}";
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
                return new ApiResult<PagedResult<ServiceTicketListDto>> { Message = $"Lỗi: {ex.Message}" };
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
                return new ApiResult<ServiceTicketDetailDto> { Message = $"Lỗi: {ex.Message}" };
            }
        }

        public async Task<ApiResult<ServiceTicketDetailDto>> AssignTechnicianAsync(int ticketId, AssignTechnicianRequest request)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"api/service-tickets/{ticketId}/assign", request);
                return await response.Content.ReadFromJsonAsync<ApiResult<ServiceTicketDetailDto>>()
                    ?? new ApiResult<ServiceTicketDetailDto> { Message = "Lỗi gán kỹ thuật viên." };
            }
            catch (Exception ex)
            {
                return new ApiResult<ServiceTicketDetailDto> { Message = $"Lỗi: {ex.Message}" };
            }
        }

        public async Task<ApiResult<ServiceTicketDetailDto>> RecordDiagnosisAsync(int ticketId, RecordDiagnosisRequest request)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"api/service-tickets/{ticketId}/diagnosis", request);
                return await response.Content.ReadFromJsonAsync<ApiResult<ServiceTicketDetailDto>>()
                    ?? new ApiResult<ServiceTicketDetailDto> { Message = "Lỗi ghi kết luận chẩn đoán." };
            }
            catch (Exception ex)
            {
                return new ApiResult<ServiceTicketDetailDto> { Message = $"Lỗi: {ex.Message}" };
            }
        }

        public async Task<ApiResult<ServiceTicketDetailDto>> ChooseBranchAsync(int ticketId, ChooseBranchRequest request)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"api/service-tickets/{ticketId}/branch", request);
                return await response.Content.ReadFromJsonAsync<ApiResult<ServiceTicketDetailDto>>()
                    ?? new ApiResult<ServiceTicketDetailDto> { Message = "Lỗi chọn loại sửa chữa." };
            }
            catch (Exception ex)
            {
                return new ApiResult<ServiceTicketDetailDto> { Message = $"Lỗi: {ex.Message}" };
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
                return new ApiResult<QuotationDto> { Message = $"Lỗi: {ex.Message}" };
            }
        }

        public async Task<ApiResult<ServiceTicketDetailDto>> AcceptQuotationAsync(int ticketId, int quotationId, AcceptQuotationRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"api/service-tickets/{ticketId}/quotation/{quotationId}/accept", request);
                return await response.Content.ReadFromJsonAsync<ApiResult<ServiceTicketDetailDto>>()
                    ?? new ApiResult<ServiceTicketDetailDto> { Message = "Lỗi duyệt báo giá." };
            }
            catch (Exception ex)
            {
                return new ApiResult<ServiceTicketDetailDto> { Message = $"Lỗi: {ex.Message}" };
            }
        }

        public async Task<ApiResult<ServiceTicketDetailDto>> RejectQuotationAsync(int ticketId, int quotationId, RejectQuotationRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"api/service-tickets/{ticketId}/quotation/{quotationId}/reject", request);
                return await response.Content.ReadFromJsonAsync<ApiResult<ServiceTicketDetailDto>>()
                    ?? new ApiResult<ServiceTicketDetailDto> { Message = "Lỗi từ chối báo giá." };
            }
            catch (Exception ex)
            {
                return new ApiResult<ServiceTicketDetailDto> { Message = $"Lỗi: {ex.Message}" };
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
                return new ApiResult<RmaShipmentDto> { Message = $"Lỗi: {ex.Message}" };
            }
        }

        public async Task<ApiResult<ServiceTicketDetailDto>> RecordRmaResolutionAsync(int ticketId, UpdateRmaResolutionRequest request)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"api/service-tickets/{ticketId}/rma/resolution", request);
                return await response.Content.ReadFromJsonAsync<ApiResult<ServiceTicketDetailDto>>()
                    ?? new ApiResult<ServiceTicketDetailDto> { Message = "Lỗi cập nhật kết quả RMA." };
            }
            catch (Exception ex)
            {
                return new ApiResult<ServiceTicketDetailDto> { Message = $"Lỗi: {ex.Message}" };
            }
        }

        public async Task<ApiResult<ServiceTicketDetailDto>> Perform1For1SwapAsync(int ticketId, Perform1For1SwapRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"api/service-tickets/{ticketId}/swap", request);
                return await response.Content.ReadFromJsonAsync<ApiResult<ServiceTicketDetailDto>>()
                    ?? new ApiResult<ServiceTicketDetailDto> { Message = "Lỗi thực hiện đổi 1-1." };
            }
            catch (Exception ex)
            {
                return new ApiResult<ServiceTicketDetailDto> { Message = $"Lỗi: {ex.Message}" };
            }
        }

        public async Task<ApiResult<ServiceTicketDetailDto>> MarkWaitingPartsAsync(int ticketId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"api/service-tickets/{ticketId}/waiting-parts", null);
                return await response.Content.ReadFromJsonAsync<ApiResult<ServiceTicketDetailDto>>()
                    ?? new ApiResult<ServiceTicketDetailDto> { Message = "Lỗi ghi nhận chờ phụ tùng." };
            }
            catch (Exception ex)
            {
                return new ApiResult<ServiceTicketDetailDto> { Message = $"Lỗi: {ex.Message}" };
            }
        }

        public async Task<ApiResult<ServiceTicketDetailDto>> ResumeRepairAsync(int ticketId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"api/service-tickets/{ticketId}/resume-repair", null);
                return await response.Content.ReadFromJsonAsync<ApiResult<ServiceTicketDetailDto>>()
                    ?? new ApiResult<ServiceTicketDetailDto> { Message = "Lỗi tiếp tục sửa chữa." };
            }
            catch (Exception ex)
            {
                return new ApiResult<ServiceTicketDetailDto> { Message = $"Lỗi: {ex.Message}" };
            }
        }

        public async Task<ApiResult<ServiceTicketDetailDto>> CompleteRepairAsync(int ticketId, CompleteRepairRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"api/service-tickets/{ticketId}/complete", request);
                return await response.Content.ReadFromJsonAsync<ApiResult<ServiceTicketDetailDto>>()
                    ?? new ApiResult<ServiceTicketDetailDto> { Message = "Lỗi hoàn tất sửa chữa." };
            }
            catch (Exception ex)
            {
                return new ApiResult<ServiceTicketDetailDto> { Message = $"Lỗi: {ex.Message}" };
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
                return new ApiResult<ServiceInvoiceDetailDto> { Message = $"Lỗi: {ex.Message}" };
            }
        }

        public async Task<ApiResult<ServiceTicketDetailDto>> CancelTicketAsync(int ticketId, CancelTicketRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"api/service-tickets/{ticketId}/cancel", request);
                return await response.Content.ReadFromJsonAsync<ApiResult<ServiceTicketDetailDto>>()
                    ?? new ApiResult<ServiceTicketDetailDto> { Message = "Lỗi hủy phiếu." };
            }
            catch (Exception ex)
            {
                return new ApiResult<ServiceTicketDetailDto> { Message = $"Lỗi: {ex.Message}" };
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
                return new ApiResult<List<ServiceTicketStatusHistoryDto>> { Message = $"Lỗi: {ex.Message}" };
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
                return new ApiResult<List<SerialRepairLogDto>> { Message = $"Lỗi: {ex.Message}" };
            }
        }
    }
}
