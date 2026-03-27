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
    public class VoucherRepository : IVoucherRepository
    {
        private readonly HushStoreDbContext _dbContext;

        public VoucherRepository(HushStoreDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<Voucher>> GetByCodesAsync(List<string> codes)
        {
            // Optimize: convert to lowercase/uppercase if needed, but depend on DB collation.
            return await _dbContext.Vouchers
                .Where(v => codes.Contains(v.Code))
                .ToListAsync();
        }

        public async Task<List<int>> GetUsedVoucherIdsByUserAsync(Guid userId, List<int> voucherIds)
        {
            return await _dbContext.VoucherUsages
                .Where(vu => vu.UserId == userId && voucherIds.Contains(vu.VoucherId))
                .Select(vu => vu.VoucherId)
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
