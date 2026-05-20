using PBL3.Shared.DTOs.ServiceTickets;

namespace PBL3.Service.ServiceTickets
{
    public interface IServiceTicketService
    {
        Task<ServiceTicketIntakeEvaluationDto> GetWarrantyEvaluationAsync(string serialNumber);
        Task<ServiceTicketDetailDto?> CreateTicketFromSerialScanAsync(ServiceTicketIntakeRequestDto request, Guid userId);
        Task<bool> AssignTechnicianAsync(int ticketId, Guid employeeId, Guid userId);
        Task<bool> RecordDiagnosisAsync(int ticketId, ServiceTicketDiagnosisDto request, Guid userId, bool isAdmin = false);
        Task<bool> ChooseBranchAsync(int ticketId, ServiceTicketBranchDto request, Guid userId, bool isAdmin = false);
        Task<QuotationDetailDto?> CreateQuotationAsync(int ticketId, QuotationCreateDto request, Guid userId, bool isAdmin = false);
        Task<bool> AcceptQuotationAsync(int ticketId, int quotationId, QuotationAcceptDto nextStatus, Guid userId, bool isAdmin = false);
        Task<bool> RejectQuotationAsync(int ticketId, int quotationId, QuotationRejectDto request, Guid userId, bool isAdmin = false);
        Task<RmaShipmentDetailDto?> CreateRmaShipmentAsync(int ticketId, RmaShipmentCreateDto request, Guid userId, bool isAdmin = false);
        Task<bool> RecordRmaResolutionAsync(int ticketId, RmaResolutionUpdateDto request, Guid userId, bool isAdmin = false);
        Task<bool> Perform1For1SwapAsync(int ticketId, int newSerialId, Guid userId, bool isAdmin = false);
        Task<bool> MarkInternalRepairCompletedAsync(int ticketId, ServiceTicketCompleteDto request, Guid userId, bool isAdmin = false);
        Task<bool> StartRepairAsync(int ticketId, Guid userId, bool isAdmin = false);
        Task<bool> MarkWaitingPartsAsync(int ticketId, Guid userId, bool isAdmin = false);
        Task<bool> ResumeRepairAsync(int ticketId, Guid userId, bool isAdmin = false);
        Task<ServiceInvoiceDetailDto?> IssueServiceInvoiceAsync(int ticketId, ServiceInvoiceCreateDto request, Guid userId, bool isAdmin = false);
        Task<bool> CancelTicketAsync(int ticketId, string reason, Guid userId);
        Task<ServiceTicketDetailDto?> GetTicketByIdAsync(int id, Guid? currentUserId = null, bool isCustomer = false);
        Task<(List<ServiceTicketListDto> Items, int TotalCount)> GetPagedTicketsAsync(
            string? keyword, byte? status, byte? resolutionType, Guid? assignedEmployeeId, Guid? customerId,
            DateTime? fromDate, DateTime? toDate, int pageNumber, int pageSize, string? sortBy, bool sortDescending);
        Task<(List<ServiceTicketListDto> Items, int TotalCount)> GetMyTicketsAsync(
            Guid userId, string? keyword, byte? status, int pageNumber, int pageSize, string? sortBy, bool sortDescending);
        Task<List<ServiceTicketStatusHistoryDto>> GetTicketHistoryAsync(int ticketId);
        Task<List<SerialRepairHistoryDto>> GetSerialRepairHistoryAsync(string serialNumber);
    }
}
