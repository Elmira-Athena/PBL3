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
            string? keyword, byte? status, int pageNumber, int pageSize, string? sortBy, bool sortDescending);
        Task<ApiResult<ServiceTicketDetailDto>> GetByIdAsync(int id);
        Task<ApiResult<bool>> AssignTechnicianAsync(int ticketId, AssignTechnicianRequest request);
        Task<ApiResult<bool>> RecordDiagnosisAsync(int ticketId, RecordDiagnosisRequest request);
        Task<ApiResult<bool>> ChooseBranchAsync(int ticketId, ChooseBranchRequest request);
        Task<ApiResult<QuotationDto>> CreateQuotationAsync(int ticketId, CreateQuotationRequest request);
        Task<ApiResult<bool>> AcceptQuotationAsync(int ticketId, int quotationId, AcceptQuotationRequest request);
        Task<ApiResult<bool>> RejectQuotationAsync(int ticketId, int quotationId, RejectQuotationRequest request);
        Task<ApiResult<RmaShipmentDto>> CreateRmaShipmentAsync(int ticketId, CreateRmaShipmentRequest request);
        Task<ApiResult<bool>> RecordRmaResolutionAsync(int ticketId, UpdateRmaResolutionRequest request);
        Task<ApiResult<bool>> Perform1For1SwapAsync(int ticketId, Perform1For1SwapRequest request);
        Task<ApiResult<bool>> StartRepairAsync(int ticketId);
        Task<ApiResult<bool>> MarkWaitingPartsAsync(int ticketId);
        Task<ApiResult<bool>> ResumeRepairAsync(int ticketId);
        Task<ApiResult<bool>> CompleteRepairAsync(int ticketId, CompleteRepairRequest request);
        Task<ApiResult<ServiceInvoiceDetailDto>> IssueServiceInvoiceAsync(int ticketId, IssueServiceInvoiceRequest request);
        Task<ApiResult<bool>> CancelTicketAsync(int ticketId, CancelTicketRequest request);
        Task<ApiResult<List<ServiceTicketStatusHistoryDto>>> GetHistoryAsync(int ticketId);
        Task<ApiResult<List<SerialRepairLogDto>>> GetSerialRepairHistoryAsync(string serialNumber);
    }
}
