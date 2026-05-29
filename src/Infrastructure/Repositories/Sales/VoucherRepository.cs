using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;

namespace PBL3.Infrastructure.Repositories
{
    public class VoucherRepository(HushStoreDbContext dbContext) : IVoucherRepository
    {
        private readonly HushStoreDbContext _dbContext =
            dbContext ?? throw new ArgumentNullException(nameof(dbContext));

        // ========================================================
        // MANAGEMENT CRUD
        // ========================================================

        public async Task<(List<Voucher> Items, int TotalCount)> GetPagedListAsync(
            string? keyword,
            string? statusFilter,
            DateTime? fromDate,
            DateTime? toDate,
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending)
        {
            var query = _dbContext.Vouchers.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(v =>
                    v.Code.ToLower().Contains(kw) ||
                    v.Name.ToLower().Contains(kw));
            }

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                var now = DateTime.UtcNow;
                query = statusFilter.ToLower() switch
                {
                    "active"    => query.Where(v => v.IsActive && v.StartDate <= now && v.EndDate >= now
                                       && (v.Quantity == null || v.UsedCount < v.Quantity)),
                    "upcoming"  => query.Where(v => v.IsActive && v.StartDate > now),
                    "expired"   => query.Where(v => v.EndDate < now),
                    "exhausted" => query.Where(v => v.Quantity.HasValue && v.UsedCount >= v.Quantity),
                    "paused"    => query.Where(v => !v.IsActive),
                    _           => query
                };
            }

            if (fromDate.HasValue)
                query = query.Where(v => v.EndDate >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(v => v.StartDate <= toDate.Value);

            var totalCount = await query.CountAsync();

            query = sortBy?.ToLower() switch
            {
                "code"      => sortDescending ? query.OrderByDescending(v => v.Code) : query.OrderBy(v => v.Code),
                "name"      => sortDescending ? query.OrderByDescending(v => v.Name) : query.OrderBy(v => v.Name),
                "startdate" => sortDescending ? query.OrderByDescending(v => v.StartDate) : query.OrderBy(v => v.StartDate),
                "enddate"   => sortDescending ? query.OrderByDescending(v => v.EndDate) : query.OrderBy(v => v.EndDate),
                "usedcount" => sortDescending ? query.OrderByDescending(v => v.UsedCount) : query.OrderBy(v => v.UsedCount),
                _           => query.OrderByDescending(v => v.CreatedDate)
            };

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Include(v => v.VoucherCategories)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<Voucher?> GetByIdWithCategoriesAsync(int id)
        {
            return await _dbContext.Vouchers
                .Include(v => v.VoucherCategories)
                .FirstOrDefaultAsync(v => v.Id == id);
        }

        public async Task<Voucher?> GetByIdNoTrackingAsync(int id)
        {
            return await _dbContext.Vouchers
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == id);
        }

        public async Task<bool> IsDuplicateCodeAsync(string code, int? excludeId = null)
        {
            var normalized = code.Trim().ToUpper();
            return await _dbContext.Vouchers
                .AnyAsync(v =>
                    v.Code.ToUpper() == normalized &&
                    (excludeId == null || v.Id != excludeId));
        }

        public async Task AddAsync(Voucher voucher)
        {
            await _dbContext.Vouchers.AddAsync(voucher);
        }

        // ========================================================
        // CHECKOUT USAGE
        // ========================================================

        public async Task<List<Voucher>> GetByCodesWithCategoriesAsync(List<string> codes)
        {
            return await _dbContext.Vouchers
                .Include(v => v.VoucherCategories)
                .Where(v => codes.Contains(v.Code))
                .ToListAsync();
        }

        public async Task<List<Voucher>> GetByCodesAsync(List<string> codes)
        {
            return await _dbContext.Vouchers
                .Where(v => codes.Contains(v.Code))
                .ToListAsync();
        }

        public async Task<Dictionary<int, int>> GetUserVoucherUsageCountsAsync(Guid userId, List<int> voucherIds)
        {
            return await _dbContext.VoucherUsages
                .Where(vu => vu.UserId == userId && voucherIds.Contains(vu.VoucherId))
                .GroupBy(vu => vu.VoucherId)
                .Select(g => new { VoucherId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.VoucherId, x => x.Count);
        }

        public async Task<List<int>> GetUsedVoucherIdsByUserAsync(Guid userId, List<int> voucherIds)
        {
            return await _dbContext.VoucherUsages
                .Where(vu => vu.UserId == userId && voucherIds.Contains(vu.VoucherId))
                .Select(vu => vu.VoucherId)
                .Distinct()
                .ToListAsync();
        }

        public async Task<List<Voucher>> GetActiveVouchersForCustomerAsync()
        {
            var now = DateTime.UtcNow;
            return await _dbContext.Vouchers
                .AsNoTracking()
                .Include(v => v.VoucherCategories)
                .Where(v => v.IsActive
                    && v.StartDate <= now && v.EndDate >= now
                    && (v.Quantity == null || v.UsedCount < v.Quantity))
                .ToListAsync();
        }

        public async Task AddUsagesAsync(IEnumerable<VoucherUsage> usages)
        {
            await _dbContext.VoucherUsages.AddRangeAsync(usages);
        }

        public async Task SaveChangesAsync()
        {
            await _dbContext.SaveChangesAsync();
        }
    }
}
