using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBL3.Service.ServiceInvoices;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.ServiceTickets;

namespace PBL3.API.Controllers.Admin
{
    [ApiController]
    [Route("api/service-invoices")]
    [Authorize(Roles = "Admin, Employee")]
    public class ServiceInvoicesController : ControllerBase
    {
        private readonly IServiceInvoiceService _service;
        private readonly ILogger<ServiceInvoicesController> _logger;

        public ServiceInvoicesController(IServiceInvoiceService service, ILogger<ServiceInvoicesController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ApiResult<PagedResult<ServiceInvoiceListDto>>> GetList(
            [FromQuery] string? keyword,
            [FromQuery] byte? paymentStatus,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? sortBy = "IssuedDate",
            [FromQuery] bool sortDescending = true)
        {
            try
            {
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;

                var (items, totalCount) = await _service.GetPagedListAsync(
                    keyword, paymentStatus, fromDate, toDate, pageNumber, pageSize, sortBy, sortDescending);
                var result = new PagedResult<ServiceInvoiceListDto>
                {
                    Items = items,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
                return ApiResult<PagedResult<ServiceInvoiceListDto>>.Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách hóa đơn dịch vụ.");
                return ApiResult<PagedResult<ServiceInvoiceListDto>>.Fail("Lỗi khi lấy danh sách hóa đơn dịch vụ.");
            }
        }

        [HttpGet("{id:int}")]
        public async Task<ApiResult<ServiceInvoiceDetailDto>> GetById(int id)
        {
            try
            {
                if (id <= 0)
                    return ApiResult<ServiceInvoiceDetailDto>.Fail("ID hóa đơn không hợp lệ.");

                var invoice = await _service.GetByIdAsync(id);
                if (invoice == null)
                    return ApiResult<ServiceInvoiceDetailDto>.Fail("Không tìm thấy hóa đơn dịch vụ yêu cầu.");

                return ApiResult<ServiceInvoiceDetailDto>.Ok(invoice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy chi tiết hóa đơn dịch vụ {InvoiceId}.", id);
                return ApiResult<ServiceInvoiceDetailDto>.Fail("Lỗi khi lấy chi tiết hóa đơn dịch vụ.");
            }
        }
    }
}
