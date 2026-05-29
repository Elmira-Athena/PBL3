using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;

namespace PBL3.Infrastructure.Repositories
{
    public class WarrantyRepository : IWarrantyRepository
    {
        private readonly HushStoreDbContext _dbContext;

        public WarrantyRepository(HushStoreDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<Warranty>> GetActiveBySerialIdAsync(int serialId)
        {
            return await _dbContext.Warranties
                .Where(w => w.SerialId == serialId && w.Status != 2) // 2 = Claimed
                .OrderByDescending(w => w.EndDate)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Warranty?> GetByIdWithTrackingAsync(int id)
        {
            return await _dbContext.Warranties
                .FirstOrDefaultAsync(w => w.Id == id);
        }

        public async Task AddAsync(Warranty warranty)
        {
            await _dbContext.Warranties.AddAsync(warranty);
        }

        public async Task AddRangeAsync(IEnumerable<Warranty> warranties)
        {
            await _dbContext.Warranties.AddRangeAsync(warranties);
        }

        public async Task SaveChangesAsync()
        {
            await _dbContext.SaveChangesAsync();
        }
    }
}
