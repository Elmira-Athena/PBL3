using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;

namespace PBL3.Infrastructure.Repositories
{
    public class QuotationRepository : IQuotationRepository
    {
        private readonly HushStoreDbContext _dbContext;

        public QuotationRepository(HushStoreDbContext dbContext)
        {
            _dbContext = dbContext;
        }

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

        public async Task<List<Quotation>> GetByTicketIdReadOnlyAsync(int ticketId)
        {
            return await _dbContext.Quotations
                .Where(q => q.TicketId == ticketId)
                .Include(q => q.Items)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<int> MarkPendingAsSupersededAsync(int ticketId)
        {
            // Một câu UPDATE duy nhất, nguyên tử. Vị từ Status == 0 nằm CÙNG câu lệnh
            // với phép gán, nên không còn khe check-then-act: dưới READ COMMITTED,
            // DB lấy row lock rồi ĐÁNH GIÁ LẠI vị từ trên bản mới nhất.
            //
            // ExecuteUpdateAsync chạy ngay lập tức (không đợi SaveChangesAsync) và
            // dùng chung DbContext — tức chung transaction — với UnitOfWork.
            return await _dbContext.Quotations
                .Where(q => q.TicketId == ticketId && q.Status == (byte)0)
                .ExecuteUpdateAsync(s => s.SetProperty(q => q.Status, (byte)3));
        }

        public async Task<bool> TryDecideAsync(int quotationId, byte fromStatus, byte toStatus, DateTime decidedAt, string? note)
        {
            var query = _dbContext.Quotations
                .Where(q => q.Id == quotationId && q.Status == fromStatus);

            // Tách hai nhánh thay vì luôn ghi note: nhánh duyệt không đụng tới
            // CustomerDecisionNote, truyền null vào sẽ XOÁ note đang có.
            var affected = note is null
                ? await query.ExecuteUpdateAsync(s => s
                    .SetProperty(q => q.Status, toStatus)
                    .SetProperty(q => q.CustomerDecidedAt, decidedAt))
                : await query.ExecuteUpdateAsync(s => s
                    .SetProperty(q => q.Status, toStatus)
                    .SetProperty(q => q.CustomerDecidedAt, decidedAt)
                    .SetProperty(q => q.CustomerDecisionNote, note));

            return affected > 0;
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
