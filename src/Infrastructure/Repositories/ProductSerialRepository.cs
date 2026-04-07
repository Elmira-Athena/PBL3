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

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
