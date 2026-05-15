using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBL3.Service.ServiceTickets;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.ServiceTickets;

namespace PBL3.API.Controllers
{
    [ApiController]
    [Route("api/service-tickets")]
    [Authorize(Roles = "Admin, Employee")]
    public class ServiceTicketsController : ControllerBase
    {
        private readonly IServiceTicketService _service;
        private readonly ILogger<ServiceTicketsController> _logger;

        public ServiceTicketsController(IServiceTicketService service, ILogger<ServiceTicketsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                throw new UnauthorizedAccessException("Không tìm thấy thông tin người dùng.");
            return userId;
        }

        private bool IsCustomer => User.IsInRole("Customer");

        [HttpPost("intake")]
        [AllowAnonymous]
        public async Task<ApiResult<ServiceTicketIntakeEvaluationDto>> EvaluateIntake([FromBody] string serialNumber)
        {
            try
            {
                if (string.IsNullOrEmpty(serialNumber))
                    return ApiResult<ServiceTicketIntakeEvaluationDto>.Fail("Mã Serial không được để trống.");

                var result = await _service.GetWarrantyEvaluationAsync(serialNumber);
                if (result.BlockingReason != null)
                    return ApiResult<ServiceTicketIntakeEvaluationDto>.Fail(result.BlockingReason);

                return ApiResult<ServiceTicketIntakeEvaluationDto>.Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating intake");
                return ApiResult<ServiceTicketIntakeEvaluationDto>.Fail("Lỗi khi kiểm tra Serial: " + ex.Message);
            }
        }

        [HttpPost]
        public async Task<ApiResult<ServiceTicketDetailDto>> CreateTicket([FromBody] ServiceTicketIntakeRequestDto request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var ticket = await _service.CreateTicketFromSerialScanAsync(request, userId);
                return ApiResult<ServiceTicketDetailDto>.Ok(ticket, "Tạo phiếu sửa chữa thành công.");
            }
            catch (InvalidOperationException ex)
            {
                return ApiResult<ServiceTicketDetailDto>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating ticket");
                return ApiResult<ServiceTicketDetailDto>.Fail("Lỗi khi tạo phiếu: " + ex.Message);
            }
        }

        [HttpGet]
        public async Task<ApiResult<PagedResult<ServiceTicketListDto>>> GetTickets(
            [FromQuery] string? keyword,
            [FromQuery] byte? status,
            [FromQuery] byte? resolutionType,
            [FromQuery] Guid? assignedEmployeeId,
            [FromQuery] Guid? customerId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? sortBy = "code",
            [FromQuery] bool sortDescending = true)
        {
            try
            {
                var (items, totalCount) = await _service.GetPagedTicketsAsync(
                    keyword, status, resolutionType, assignedEmployeeId, customerId,
                    fromDate, toDate, pageNumber, pageSize, sortBy, sortDescending);

                var paged = new PagedResult<ServiceTicketListDto>
                {
                    Items = items,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };

                return ApiResult<PagedResult<ServiceTicketListDto>>.Ok(paged);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing tickets");
                return ApiResult<PagedResult<ServiceTicketListDto>>.Fail("Lỗi khi tải danh sách: " + ex.Message);
            }
        }

        [HttpGet("my")]
        [Authorize(Roles = "Customer")]
        public async Task<ApiResult<PagedResult<ServiceTicketListDto>>> GetMyTickets(
            [FromQuery] string? keyword,
            [FromQuery] byte? status,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? sortBy = "date",
            [FromQuery] bool sortDescending = true)
        {
            try
            {
                var userId = GetCurrentUserId();
                var (items, totalCount) = await _service.GetMyTicketsAsync(
                    userId, keyword, status, pageNumber, pageSize, sortBy, sortDescending);

                var paged = new PagedResult<ServiceTicketListDto>
                {
                    Items = items,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };

                return ApiResult<PagedResult<ServiceTicketListDto>>.Ok(paged);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing customer tickets");
                return ApiResult<PagedResult<ServiceTicketListDto>>.Fail("Lỗi khi tải danh sách: " + ex.Message);
            }
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Admin, Employee, Customer")]
        public async Task<ApiResult<ServiceTicketDetailDto>> GetTicketDetail(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var ticket = await _service.GetTicketByIdAsync(id, userId, IsCustomer);
                if (ticket == null)
                    return ApiResult<ServiceTicketDetailDto>.Fail("Không tìm thấy phiếu.");

                return ApiResult<ServiceTicketDetailDto>.Ok(ticket);
            }
            catch (UnauthorizedAccessException ex)
            {
                return ApiResult<ServiceTicketDetailDto>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting ticket detail");
                return ApiResult<ServiceTicketDetailDto>.Fail("Lỗi khi tải chi tiết: " + ex.Message);
            }
        }

        [HttpPut("{id}/assign")]
        public async Task<ApiResult<bool>> AssignTechnician(int id, [FromBody] ServiceTicketAssignDto request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _service.AssignTechnicianAsync(id, request.EmployeeId, userId);
                return ApiResult<bool>.Ok(result, "Giao phó kỹ thuật viên thành công.");
            }
            catch (InvalidOperationException ex)
            {
                return ApiResult<bool>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning technician");
                return ApiResult<bool>.Fail("Lỗi khi giao phó: " + ex.Message);
            }
        }

        [HttpPut("{id}/diagnosis")]
        public async Task<ApiResult<bool>> RecordDiagnosis(int id, [FromBody] ServiceTicketDiagnosisDto request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _service.RecordDiagnosisAsync(id, request, userId);
                return ApiResult<bool>.Ok(result, "Ghi nhận chẩn đoán thành công.");
            }
            catch (InvalidOperationException ex)
            {
                return ApiResult<bool>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording diagnosis");
                return ApiResult<bool>.Fail("Lỗi khi ghi nhận: " + ex.Message);
            }
        }

        [HttpPut("{id}/branch")]
        public async Task<ApiResult<bool>> ChooseBranch(int id, [FromBody] ServiceTicketBranchDto request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _service.ChooseBranchAsync(id, request, userId);
                return ApiResult<bool>.Ok(result, "Chọn loại giải pháp thành công.");
            }
            catch (InvalidOperationException ex)
            {
                return ApiResult<bool>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error choosing branch");
                return ApiResult<bool>.Fail("Lỗi khi chọn giải pháp: " + ex.Message);
            }
        }

        [HttpPost("{id}/quotation")]
        public async Task<ApiResult<QuotationDetailDto>> CreateQuotation(int id, [FromBody] QuotationCreateDto request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var quotation = await _service.CreateQuotationAsync(id, request, userId);
                return ApiResult<QuotationDetailDto>.Ok(quotation, "Tạo báo giá thành công.");
            }
            catch (InvalidOperationException ex)
            {
                return ApiResult<QuotationDetailDto>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating quotation");
                return ApiResult<QuotationDetailDto>.Fail("Lỗi khi tạo báo giá: " + ex.Message);
            }
        }

        [HttpPost("{id}/quotation/{qid}/accept")]
        [Authorize(Roles = "Admin, Employee, Customer")]
        public async Task<ApiResult<bool>> AcceptQuotation(int id, int qid, [FromBody] QuotationAcceptDto request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _service.AcceptQuotationAsync(id, qid, request, userId);
                return ApiResult<bool>.Ok(result, "Chấp nhận báo giá thành công.");
            }
            catch (UnauthorizedAccessException ex)
            {
                return ApiResult<bool>.Fail(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return ApiResult<bool>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting quotation");
                return ApiResult<bool>.Fail("Lỗi khi chấp nhận: " + ex.Message);
            }
        }

        [HttpPost("{id}/quotation/{qid}/reject")]
        [Authorize(Roles = "Admin, Employee, Customer")]
        public async Task<ApiResult<bool>> RejectQuotation(int id, int qid, [FromBody] QuotationRejectDto request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _service.RejectQuotationAsync(id, qid, request, userId);
                return ApiResult<bool>.Ok(result, "Từ chối báo giá thành công.");
            }
            catch (UnauthorizedAccessException ex)
            {
                return ApiResult<bool>.Fail(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return ApiResult<bool>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting quotation");
                return ApiResult<bool>.Fail("Lỗi khi từ chối: " + ex.Message);
            }
        }

        [HttpPost("{id}/rma")]
        public async Task<ApiResult<RmaShipmentDetailDto>> CreateRmaShipment(int id, [FromBody] RmaShipmentCreateDto request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var rma = await _service.CreateRmaShipmentAsync(id, request, userId);
                return ApiResult<RmaShipmentDetailDto>.Ok(rma, "Tạo phiếu RMA thành công.");
            }
            catch (InvalidOperationException ex)
            {
                return ApiResult<RmaShipmentDetailDto>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating RMA");
                return ApiResult<RmaShipmentDetailDto>.Fail("Lỗi khi tạo RMA: " + ex.Message);
            }
        }

        [HttpPut("{id}/rma/resolution")]
        public async Task<ApiResult<bool>> UpdateRmaResolution(int id, [FromBody] RmaResolutionUpdateDto request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _service.RecordRmaResolutionAsync(id, request, userId);
                return ApiResult<bool>.Ok(result, "Cập nhật kết quả RMA thành công.");
            }
            catch (InvalidOperationException ex)
            {
                return ApiResult<bool>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating RMA resolution");
                return ApiResult<bool>.Fail("Lỗi khi cập nhật: " + ex.Message);
            }
        }

        [HttpPost("{id}/swap")]
        public async Task<ApiResult<bool>> Perform1For1Swap(int id, [FromBody] int newSerialId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _service.Perform1For1SwapAsync(id, newSerialId, userId);
                return ApiResult<bool>.Ok(result, "Đổi 1-1 thành công.");
            }
            catch (InvalidOperationException ex)
            {
                return ApiResult<bool>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing swap");
                return ApiResult<bool>.Fail("Lỗi khi đổi 1-1: " + ex.Message);
            }
        }

        [HttpPost("{id}/waiting-parts")]
        public async Task<ApiResult<bool>> MarkWaitingParts(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _service.MarkWaitingPartsAsync(id, userId);
                return ApiResult<bool>.Ok(result, "Cập nhật trạng thái thành công.");
            }
            catch (InvalidOperationException ex)
            {
                return ApiResult<bool>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking waiting parts");
                return ApiResult<bool>.Fail("Lỗi khi cập nhật: " + ex.Message);
            }
        }

        [HttpPost("{id}/resume-repair")]
        public async Task<ApiResult<bool>> ResumeRepair(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _service.ResumeRepairAsync(id, userId);
                return ApiResult<bool>.Ok(result, "Tiếp tục sửa chữa thành công.");
            }
            catch (InvalidOperationException ex)
            {
                return ApiResult<bool>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resuming repair");
                return ApiResult<bool>.Fail("Lỗi khi tiếp tục: " + ex.Message);
            }
        }

        [HttpPost("{id}/complete")]
        public async Task<ApiResult<bool>> CompleteTicket(int id, [FromBody] ServiceTicketCompleteDto request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _service.MarkInternalRepairCompletedAsync(id, request, userId);
                return ApiResult<bool>.Ok(result, "Hoàn tát sửa chữa thành công.");
            }
            catch (InvalidOperationException ex)
            {
                return ApiResult<bool>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing ticket");
                return ApiResult<bool>.Fail("Lỗi khi hoàn tất: " + ex.Message);
            }
        }

        [HttpPost("{id}/invoice")]
        public async Task<ApiResult<ServiceInvoiceDetailDto>> IssueServiceInvoice(int id, [FromBody] ServiceInvoiceCreateDto request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var invoice = await _service.IssueServiceInvoiceAsync(id, request, userId);
                return ApiResult<ServiceInvoiceDetailDto>.Ok(invoice, "Tạo hóa đơn dịch vụ thành công.");
            }
            catch (InvalidOperationException ex)
            {
                return ApiResult<ServiceInvoiceDetailDto>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error issuing invoice");
                return ApiResult<ServiceInvoiceDetailDto>.Fail("Lỗi khi tạo hóa đơn: " + ex.Message);
            }
        }

        [HttpPost("{id}/cancel")]
        [Authorize(Roles = "Admin")]
        public async Task<ApiResult<bool>> CancelTicket(int id, [FromBody] ServiceTicketCancelDto request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _service.CancelTicketAsync(id, request.CancelReason, userId);
                return ApiResult<bool>.Ok(result, "Hủy phiếu thành công.");
            }
            catch (InvalidOperationException ex)
            {
                return ApiResult<bool>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling ticket");
                return ApiResult<bool>.Fail("Lỗi khi hủy: " + ex.Message);
            }
        }

        [HttpGet("{id}/history")]
        [Authorize(Roles = "Admin, Employee, Customer")]
        public async Task<ApiResult<List<ServiceTicketStatusHistoryDto>>> GetTicketHistory(int id)
        {
            try
            {
                var history = await _service.GetTicketHistoryAsync(id);
                return ApiResult<List<ServiceTicketStatusHistoryDto>>.Ok(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting ticket history");
                return ApiResult<List<ServiceTicketStatusHistoryDto>>.Fail("Lỗi khi tải lịch sử: " + ex.Message);
            }
        }

        [HttpGet("serials/{serialNumber}/repair-history")]
        public async Task<ApiResult<List<SerialRepairHistoryDto>>> GetSerialRepairHistory(string serialNumber)
        {
            try
            {
                var history = await _service.GetSerialRepairHistoryAsync(serialNumber);
                return ApiResult<List<SerialRepairHistoryDto>>.Ok(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting serial repair history");
                return ApiResult<List<SerialRepairHistoryDto>>.Fail("Lỗi khi tải lịch sử: " + ex.Message);
            }
        }
    }
}
