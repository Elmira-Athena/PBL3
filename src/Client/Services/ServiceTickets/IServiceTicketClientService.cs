using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.ServiceTickets;

namespace Client.Services.ServiceTickets
{
    public interface IServiceTicketClientService
    {
        Task<ApiResult<ServiceTicketIntakeEvaluationDto>> EvaluateIntakeAsync(string serialNumber);
        Task<ApiResult<ServiceTicketDetailDto>> CreateTicketAsync(CreateServiceTicketRequest request);
        Task<ApiResult<PagedResult<ServiceTicketListDto>>> GetPagedListAsync(
            string? keyword, byte? status, DateTime? fromDate, DateTime? toDate,
            int pageNumber, int pageSize, string? sortBy, bool sortDescending);
        Task<ApiResult<PagedResult<ServiceTicketListDto>>> GetMyTicketsAsync(
            string? keyword, int pageNumber, int pageSize, string? sortBy, bool sortDescending);
        Task<ApiResult<ServiceTicketDetailDto>> GetByIdAsync(int id);
        Task<ApiResult<ServiceTicketDetailDto>> AssignTechnicianAsync(int ticketId, AssignTechnicianRequest request);
        Task<ApiResult<ServiceTicketDetailDto>> RecordDiagnosisAsync(int ticketId, RecordDiagnosisRequest request);
        Task<ApiResult<ServiceTicketDetailDto>> ChooseBranchAsync(int ticketId, ChooseBranchRequest request);
        Task<ApiResult<QuotationDto>> CreateQuotationAsync(int ticketId, CreateQuotationRequest request);
        Task<ApiResult<ServiceTicketDetailDto>> AcceptQuotationAsync(int ticketId, int quotationId, AcceptQuotationRequest request);
        Task<ApiResult<ServiceTicketDetailDto>> RejectQuotationAsync(int ticketId, int quotationId, RejectQuotationRequest request);
        Task<ApiResult<RmaShipmentDto>> CreateRmaShipmentAsync(int ticketId, CreateRmaShipmentRequest request);
        Task<ApiResult<ServiceTicketDetailDto>> RecordRmaResolutionAsync(int ticketId, UpdateRmaResolutionRequest request);
        Task<ApiResult<ServiceTicketDetailDto>> Perform1For1SwapAsync(int ticketId, Perform1For1SwapRequest request);
        Task<ApiResult<ServiceTicketDetailDto>> MarkWaitingPartsAsync(int ticketId);
        Task<ApiResult<ServiceTicketDetailDto>> ResumeRepairAsync(int ticketId);
        Task<ApiResult<ServiceTicketDetailDto>> CompleteRepairAsync(int ticketId, CompleteRepairRequest request);
        Task<ApiResult<ServiceInvoiceDetailDto>> IssueServiceInvoiceAsync(int ticketId, IssueServiceInvoiceRequest request);
        Task<ApiResult<ServiceTicketDetailDto>> CancelTicketAsync(int ticketId, CancelTicketRequest request);
        Task<ApiResult<List<ServiceTicketStatusHistoryDto>>> GetHistoryAsync(int ticketId);
        Task<ApiResult<List<SerialRepairLogDto>>> GetSerialRepairHistoryAsync(string serialNumber);
    }
}
