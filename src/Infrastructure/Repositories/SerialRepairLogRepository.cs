using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;

namespace PBL3.Infrastructure.Repositories
{
    public class SerialRepairLogRepository : ISerialRepairLogRepository
    {
        private readonly HushStoreDbContext _dbContext;

        public SerialRepairLogRepository(HushStoreDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<SerialRepairLog>> GetBySerialIdAsync(int serialId)
        {
            return await _dbContext.SerialRepairLogs
                .Where(l => l.SerialId == serialId)
                .OrderByDescending(l => l.LoggedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<SerialRepairLog?> GetByIdWithTrackingAsync(int id)
        {
            return await _dbContext.SerialRepairLogs
                .FirstOrDefaultAsync(l => l.Id == id);
        }

        public async Task AddAsync(SerialRepairLog log)
        {
            await _dbContext.SerialRepairLogs.AddAsync(log);
        }

        public async Task SaveChangesAsync()
        {
            await _dbContext.SaveChangesAsync();
        }
    }
}
