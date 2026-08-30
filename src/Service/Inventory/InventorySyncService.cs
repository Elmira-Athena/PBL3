using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;

namespace PBL3.Service.Inventory
{
    public class InventorySyncService : IInventorySyncService
    {
        private readonly HushStoreDbContext _context;

        public InventorySyncService(HushStoreDbContext context)
        {
            _context = context;
        }

        public async Task SyncStockAsync(int variantId)
        {
            await SyncStockBatchAsync(new[] { variantId });
        }

        public async Task SyncStockBatchAsync(IEnumerable<int> variantIds)
        {
            var ids = variantIds.Distinct().ToList();
            if (ids.Count == 0) return;

            // MỘT câu UPDATE duy nhất với subquery tương quan:
            //   UPDATE ProductVariants SET StockQuantity = (SELECT COUNT(*) FROM ProductSerials
            //                                               WHERE VariantId = ProductVariants.Id AND Status = 0)
            //   WHERE Id IN (...)
            //
            // Vì sao không tách COUNT rồi UPDATE như bản cũ:
            //   1. Bản cũ có khe race giữa bước COUNT và bước UPDATE — một serial bán ra
            //      trong khoảng đó làm StockQuantity bị ghi đè bằng con số đã cũ.
            //      Gộp vào một câu thì DB tự đánh giá COUNT tại thời điểm ghi, khe đó biến mất.
            //   2. Bản cũ gọi ExecuteUpdateAsync trong vòng foreach => N round-trip và
            //      giữ X-lock trên ProductVariants suốt N lượt. Giờ còn đúng 1 lượt.
            //
            // Variant không còn serial Available nào vẫn được set về 0 (subquery trả 0),
            // đúng như hành vi của bản cũ.
            await _context.ProductVariants
                .Where(v => ids.Contains(v.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(
                    v => v.StockQuantity,
                    v => _context.ProductSerials.Count(ps => ps.VariantId == v.Id && ps.Status == 0)));
        }
    }
}
