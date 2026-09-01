using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;

namespace PBL3.Infrastructure.Repositories
{
    public class ImportReceiptRepository : IImportReceiptRepository
    {
        private readonly HushStoreDbContext _context;

        public ImportReceiptRepository(HushStoreDbContext context)
        {
            _context = context;
        }

        public async Task<(List<ImportReceipt> Items, int TotalCount)> GetPagedListAsync(
            string? keyword,
            DateTime? fromDate,
            DateTime? toDate,
            int? supplierId,
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending)
        {
            var query = _context.ImportReceipts
                .AsNoTracking()
                .Include(r => r.Supplier)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(r =>
                    r.ReceiptCode.ToLower().Contains(kw) ||
                    r.Supplier.Name.ToLower().Contains(kw));
            }

            if (fromDate.HasValue)
                query = query.Where(r => r.ImportDate >= fromDate.Value.ToUniversalTime());

            if (toDate.HasValue)
                query = query.Where(r => r.ImportDate < toDate.Value.AddDays(1).ToUniversalTime());

            if (supplierId.HasValue)
                query = query.Where(r => r.SupplierId == supplierId.Value);

            var totalCount = await query.CountAsync();

            // Sắp xếp
            query = sortBy?.ToLower() switch
            {
                "code" => sortDescending ? query.OrderByDescending(r => r.ReceiptCode) : query.OrderBy(r => r.ReceiptCode),
                "supplier" => sortDescending ? query.OrderByDescending(r => r.Supplier.Name) : query.OrderBy(r => r.Supplier.Name),
                "total" => sortDescending ? query.OrderByDescending(r => r.TotalAmount) : query.OrderBy(r => r.TotalAmount),
                _ => sortDescending ? query.OrderByDescending(r => r.ImportDate) : query.OrderBy(r => r.ImportDate)
            };

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<ImportReceipt?> GetByIdWithDetailsAsync(int id)
        {
            return await _context.ImportReceipts
                .AsNoTracking()
                .Include(r => r.Supplier)
                .Include(r => r.Details)
                    .ThenInclude(d => d.Variant)
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task AddAsync(ImportReceipt receipt)
        {
            _context.ImportReceipts.Add(receipt);
            await Task.CompletedTask;
        }

        public async Task AddDetailAsync(ImportReceiptDetail detail)
        {
            _context.ImportReceiptDetails.Add(detail);
            await Task.CompletedTask;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
