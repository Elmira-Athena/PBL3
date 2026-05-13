using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.ServiceTickets;

namespace Client.Services.ServiceTickets
{
    public interface IServiceInvoiceClientService
    {
        Task<ApiResult<PagedResult<ServiceInvoiceListDto>>> GetPagedListAsync(
            string? keyword, int pageNumber, int pageSize, string? sortBy, bool sortDescending);
        Task<ApiResult<ServiceInvoiceDetailDto>> GetByIdAsync(int id);
    }
}
