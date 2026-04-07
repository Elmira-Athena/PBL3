using System.Collections.Generic;
using System.Threading.Tasks;
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
