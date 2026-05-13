using PBL3.Shared.DTOs.ServiceTickets;

namespace PBL3.Service.ServiceInvoices
{
    public interface IServiceInvoiceService
    {
        Task<ServiceInvoiceDetailDto?> GetByIdAsync(int id);
        Task<ServiceInvoiceDetailDto?> GetByTicketIdAsync(int ticketId);
        Task<(List<ServiceInvoiceListDto> Items, int TotalCount)> GetPagedListAsync(
            string? keyword, int pageNumber, int pageSize, string? sortBy, bool sortDescending);
    }
}
