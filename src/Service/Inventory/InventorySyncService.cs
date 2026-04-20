using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;

namespace PBL3.Service.Inventory
{
    public class InventorySyncService(HushStoreDbContext context) : IInventorySyncService
    {
        private readonly HushStoreDbContext _context = context;

        public async Task SyncStockAsync(int variantId)
        {
            // 1. Count Serials Available (Status = 0)
            var availableCount = await _context.ProductSerials
                .CountAsync(s => s.VariantId == variantId && s.Status == 0);

            // 2. Update cột vật lý — ExecuteUpdateAsync (Bulk update, KHÔNG load data lên memory)
            await _context.ProductVariants
                .Where(v => v.Id == variantId)
                .ExecuteUpdateAsync(s =>
                    s.SetProperty(v => v.StockQuantity, availableCount));
        }

        public async Task SyncStockBatchAsync(IEnumerable<int> variantIds)
        {
            var ids = variantIds.Distinct().ToList();
            if (ids.Count == 0) return;

            // 1. Batch COUNT — 1 query duy nhất GROUP BY cho tất cả variants
            var stockCounts = await _context.ProductSerials
                .Where(s => ids.Contains(s.VariantId) && s.Status == 0)
                .GroupBy(s => s.VariantId)
                .Select(g => new { VariantId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.VariantId, x => x.Count);

            // 2. Bulk Update — xử lý từng variant (kể cả những variant có 0 serial cũng phải update về 0)
            foreach (var variantId in ids)
            {
                var count = stockCounts.GetValueOrDefault(variantId, 0);
                
                await _context.ProductVariants
                    .Where(v => v.Id == variantId)
                    .ExecuteUpdateAsync(s =>
                        s.SetProperty(v => v.StockQuantity, count));
            }
        }
    }
}
