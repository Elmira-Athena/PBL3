using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;

namespace PBL3.Infrastructure.Repositories
{
    public class ServiceInvoiceRepository : IServiceInvoiceRepository
    {
        private readonly HushStoreDbContext _dbContext;

        public ServiceInvoiceRepository(HushStoreDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<(List<ServiceInvoice> Items, int TotalCount)> GetPagedListAsync(
            string? keyword,
            byte? paymentStatus,
            DateTime? fromDate,
            DateTime? toDate,
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending)
        {
            var query = _dbContext.ServiceInvoices.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(keyword))
                query = query.Where(i => i.InvoiceCode.Contains(keyword));

            if (paymentStatus.HasValue)
                query = query.Where(i => i.PaymentStatus == paymentStatus.Value);

            if (fromDate.HasValue)
                query = query.Where(i => i.IssuedDate >= fromDate.Value.Date);

            if (toDate.HasValue)
                query = query.Where(i => i.IssuedDate < toDate.Value.Date.AddDays(1));

            var totalCount = await query.CountAsync();

            // Sorting
            query = sortBy switch
            {
                "code" => sortDescending ? query.OrderByDescending(i => i.InvoiceCode) : query.OrderBy(i => i.InvoiceCode),
                "issued" => sortDescending ? query.OrderByDescending(i => i.IssuedDate) : query.OrderBy(i => i.IssuedDate),
                _ => query.OrderByDescending(i => i.IssuedDate)
            };

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<ServiceInvoice?> GetByIdWithDetailsAsync(int id)
        {
            return await _dbContext.ServiceInvoices
                .Include(i => i.Ticket)
                .Include(i => i.Quotation)
                .Include(i => i.Items)
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == id);
        }

        public async Task<ServiceInvoice?> GetByTicketIdAsync(int ticketId)
        {
            return await _dbContext.ServiceInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.TicketId == ticketId);
        }

        public async Task<string?> GetLastInvoiceCodeByDateAsync(string datePrefix)
        {
            return await _dbContext.ServiceInvoices
                .Where(i => i.InvoiceCode.StartsWith(datePrefix))
                .OrderByDescending(i => i.InvoiceCode)
                .Select(i => i.InvoiceCode)
                .FirstOrDefaultAsync();
        }

        public async Task<bool> InvoiceExistsForTicketAsync(int ticketId)
        {
            return await _dbContext.ServiceInvoices
                .AnyAsync(i => i.TicketId == ticketId);
        }

        public async Task AddAsync(ServiceInvoice invoice)
        {
            await _dbContext.ServiceInvoices.AddAsync(invoice);
        }

        public async Task SaveChangesAsync()
        {
            await _dbContext.SaveChangesAsync();
        }
    }
}
