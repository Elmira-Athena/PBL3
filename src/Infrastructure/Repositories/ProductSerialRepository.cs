using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;

namespace PBL3.Infrastructure.Repositories
{
    public class ProductSerialRepository : IProductSerialRepository
    {
        private readonly HushStoreDbContext _context;

        public ProductSerialRepository(HushStoreDbContext context)
        {
            _context = context;
        }

        public async Task<bool> ExistsAsync(string serialNumber, int variantId)
        {
            return await _context.ProductSerials
                .AsNoTracking()
                .AnyAsync(x => x.SerialNumber == serialNumber && x.VariantId == variantId);
        }

        public async Task<List<string>> GetExistingSerialsAsync(List<string> serialNumbers)
        {
            // Tìm các Serial đã tồn tại trong DB (so sánh case-insensitive)
            return await _context.ProductSerials
                .AsNoTracking()
                .Where(s => serialNumbers.Contains(s.SerialNumber))
                .Select(s => s.SerialNumber)
                .ToListAsync();
        }

        public async Task AddRangeAsync(IEnumerable<ProductSerial> serials)
        {
            _context.ProductSerials.AddRange(serials);
            await Task.CompletedTask;
        }

        public async Task<List<string>> GetSerialsByReceiptAndVariantAsync(int receiptId, int variantId)
        {
            return await _context.ProductSerials
                .AsNoTracking()
                .Where(s => s.ImportReceiptId == receiptId && s.VariantId == variantId)
                .Select(s => s.SerialNumber)
                .ToListAsync();
        }

        public async Task<ProductSerial?> GetBySerialNumberAsync(string serialNumber)
        {
            return await _context.ProductSerials
                .Include(s => s.Variant)
                    .ThenInclude(v => v.Product)
                .FirstOrDefaultAsync(s => s.SerialNumber == serialNumber);
        }

        public async Task<List<ProductSerial>> GetAvailableSerialsByVariantAsync(int variantId, int count)
        {
            return await _context.ProductSerials
                .Where(s => s.VariantId == variantId && s.Status == 0) // 0: Available
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<ProductSerial>> GetSerialsWithTrackingAsync(List<string> serialNumbers)
        {
            return await _context.ProductSerials
                .Include(s => s.Variant)
                .Where(s => serialNumbers.Contains(s.SerialNumber))
                .ToListAsync(); // WITH tracking (no AsNoTracking)
        }

        public async Task<ProductSerial?> GetByIdWithTrackingAsync(int id)
        {
            return await _context.ProductSerials
                .Include(s => s.Variant)
                .FirstOrDefaultAsync(s => s.Id == id); // WITH tracking (no AsNoTracking)
        }

        public async Task<Dictionary<int, int>> CountAvailableByVariantIdsAsync(List<int> variantIds)
        {
            return await _context.ProductSerials
                .AsNoTracking()
                .Where(s => s.Status == 0 && variantIds.Contains(s.VariantId))
                .GroupBy(s => s.VariantId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task<(List<ProductSerial> Items, int TotalCount)> GetPagedListAsync(
            string? keyword, int? productId, int? variantId,
            byte? status, DateTime? fromDate, DateTime? toDate,
            int pageNumber, int pageSize, string? sortBy, bool sortDescending)
        {
            var query = _context.ProductSerials
                .AsNoTracking()
                .Include(s => s.Variant).ThenInclude(v => v.Product)
                .Include(s => s.ImportReceipt)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
                query = query.Where(s => s.SerialNumber.Contains(keyword.Trim()));
            if (productId.HasValue)
                query = query.Where(s => s.Variant.ProductId == productId.Value);
            if (variantId.HasValue)
                query = query.Where(s => s.VariantId == variantId.Value);
            if (status.HasValue)
                query = query.Where(s => s.Status == status.Value);
            if (fromDate.HasValue)
                query = query.Where(s => s.CreatedDate >= fromDate.Value);
            if (toDate.HasValue)
                query = query.Where(s => s.CreatedDate <= toDate.Value);

            var totalCount = await query.CountAsync();

            query = sortBy?.ToLower() switch
            {
                "serialnumber" => sortDescending ? query.OrderByDescending(s => s.SerialNumber) : query.OrderBy(s => s.SerialNumber),
                "status"       => sortDescending ? query.OrderByDescending(s => s.Status)       : query.OrderBy(s => s.Status),
                _              => sortDescending ? query.OrderByDescending(s => s.CreatedDate)  : query.OrderBy(s => s.CreatedDate)
            };

            var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
            return (items, totalCount);
        }

        public async Task<Dictionary<byte, int>> GetStatusCountsAsync(int? productId, int? variantId)
        {
            var query = _context.ProductSerials.AsNoTracking().AsQueryable();
            if (productId.HasValue)
                query = query.Where(s => s.Variant.ProductId == productId.Value);
            if (variantId.HasValue)
                query = query.Where(s => s.VariantId == variantId.Value);

            return await query
                .GroupBy(s => s.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Status, x => x.Count);
        }

        public async Task<ProductSerial?> GetByIdWithDetailsAsync(int id)
        {
            return await _context.ProductSerials
                .AsNoTracking()
                .Include(s => s.Variant).ThenInclude(v => v.Product)
                .Include(s => s.ImportReceipt).ThenInclude(r => r.Supplier)
                .FirstOrDefaultAsync(s => s.Id == id);
        }
    }
}
