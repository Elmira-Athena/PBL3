using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;

namespace PBL3.Infrastructure.Repositories
{
    public class QuotationRepository(HushStoreDbContext dbContext) : IQuotationRepository
    {
        private readonly HushStoreDbContext _dbContext =
            dbContext ?? throw new ArgumentNullException(nameof(dbContext));

        public async Task<Quotation?> GetByIdWithItemsAsync(int id)
        {
            return await _dbContext.Quotations
                .Include(q => q.Items)
                .AsNoTracking()
                .FirstOrDefaultAsync(q => q.Id == id);
        }

        public async Task<Quotation?> GetByIdWithTrackingAsync(int id)
        {
            return await _dbContext.Quotations
                .Include(q => q.Items)
                .FirstOrDefaultAsync(q => q.Id == id);
        }

        public async Task<List<Quotation>> GetByTicketIdAsync(int ticketId)
        {
            return await _dbContext.Quotations
                .Where(q => q.TicketId == ticketId)
                .Include(q => q.Items)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<bool> HasAcceptedQuotationAsync(int ticketId)
        {
            return await _dbContext.Quotations
                .Where(q => q.TicketId == ticketId && q.Status == 1) // 1 = Accepted
                .AnyAsync();
        }

        public async Task AddAsync(Quotation quotation)
        {
            await _dbContext.Quotations.AddAsync(quotation);
        }

        public async Task SaveChangesAsync()
        {
            await _dbContext.SaveChangesAsync();
        }
    }
}
