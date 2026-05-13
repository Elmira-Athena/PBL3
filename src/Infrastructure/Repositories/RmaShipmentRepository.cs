using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;

namespace PBL3.Infrastructure.Repositories
{
    public class RmaShipmentRepository : IRmaShipmentRepository
    {
        private readonly HushStoreDbContext _dbContext;

        public RmaShipmentRepository(HushStoreDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<RmaShipment?> GetByTicketIdAsync(int ticketId)
        {
            return await _dbContext.RmaShipments
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.TicketId == ticketId);
        }

        public async Task<RmaShipment?> GetByIdWithTrackingAsync(int id)
        {
            return await _dbContext.RmaShipments
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task AddAsync(RmaShipment shipment)
        {
            await _dbContext.RmaShipments.AddAsync(shipment);
        }

        public async Task SaveChangesAsync()
        {
            await _dbContext.SaveChangesAsync();
        }
    }
}
