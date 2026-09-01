using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;

namespace PBL3.Infrastructure.Repositories
{
    public class ServiceTicketRepository : IServiceTicketRepository
    {
        private readonly HushStoreDbContext _dbContext;

        public ServiceTicketRepository(HushStoreDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<(List<ServiceTicket> Items, int TotalCount)> GetPagedListAsync(
            string? keyword,
            byte? status,
            byte? resolutionType,
            Guid? assignedEmployeeId,
            Guid? customerId,
            DateTime? fromDate,
            DateTime? toDate,
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending)
        {
            var query = _dbContext.ServiceTickets.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(t => t.TicketCode.Contains(keyword));
            }

            if (status.HasValue)
            {
                query = query.Where(t => t.Status == status.Value);
            }

            if (resolutionType.HasValue)
            {
                query = query.Where(t => t.ResolutionType == resolutionType.Value);
            }

            if (assignedEmployeeId.HasValue)
            {
                query = query.Where(t => t.AssignedEmployeeId == assignedEmployeeId.Value);
            }

            if (customerId.HasValue)
            {
                query = query.Where(t => t.CustomerId == customerId.Value);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(t => t.IntakeDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(t => t.IntakeDate <= toDate.Value);
            }

            var totalCount = await query.CountAsync();

            // Sorting
            query = sortBy switch
            {
                "code" => sortDescending ? query.OrderByDescending(t => t.TicketCode) : query.OrderBy(t => t.TicketCode),
                "status" => sortDescending ? query.OrderByDescending(t => t.Status) : query.OrderBy(t => t.Status),
                "intakeDate" => sortDescending ? query.OrderByDescending(t => t.IntakeDate) : query.OrderBy(t => t.IntakeDate),
                _ => query.OrderByDescending(t => t.CreatedDate)
            };

            var items = await query
                .Include(t => t.Serial)
                    .ThenInclude(s => s.Variant)
                        .ThenInclude(v => v.Product)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<ServiceTicket?> GetByIdAsync(int id)
        {
            return await _dbContext.ServiceTickets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<ServiceTicket?> GetByIdWithDetailsAsync(int id)
        {
            return await _dbContext.ServiceTickets
                .Include(t => t.Serial)
                    .ThenInclude(s => s.Variant)
                        .ThenInclude(v => v.Product)
                .Include(t => t.OriginalOrder)
                .Include(t => t.Customer)
                    .ThenInclude(c => c!.Profile)
                .Include(t => t.StatusHistory.OrderByDescending(h => h.ChangedAt))
                .Include(t => t.Quotations)
                    .ThenInclude(q => q.Items)
                .Include(t => t.RmaShipment)
                .Include(t => t.Invoice)
                    .ThenInclude(i => i.Items)
                .Include(t => t.ReplacementSerial)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<ServiceTicket?> GetByIdWithTrackingAsync(int id)
        {
            return await _dbContext.ServiceTickets
                .Include(t => t.Serial)
                    .ThenInclude(s => s.Variant)
                .Include(t => t.ReplacementSerial)
                .Include(t => t.RmaShipment)
                .Include(t => t.Quotations)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<bool> HasOpenTicketForSerialAsync(int serialId)
        {
            // Terminal states: 3 = QuoteRejected, 8 = Swapped, 9 = Completed, 10 = Cancelled
            var terminalStates = new[] { (byte)3, (byte)8, (byte)9, (byte)10 };
            return await _dbContext.ServiceTickets
                .Where(t => t.SerialId == serialId && !terminalStates.Contains(t.Status))
                .AnyAsync();
        }

        public async Task<(List<ServiceTicket> Items, int TotalCount)> GetTicketsByOrderUserIdAsync(
            Guid userId,
            string? keyword,
            byte? status,
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending)
        {
            var query = _dbContext.ServiceTickets
                .Where(t => t.OriginalOrder.UserId == userId)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(t => t.TicketCode.Contains(keyword));
            }

            if (status.HasValue)
            {
                query = query.Where(t => t.Status == status.Value);
            }

            var totalCount = await query.CountAsync();

            // Sorting
            query = sortBy switch
            {
                "code" => sortDescending ? query.OrderByDescending(t => t.TicketCode) : query.OrderBy(t => t.TicketCode),
                "status" => sortDescending ? query.OrderByDescending(t => t.Status) : query.OrderBy(t => t.Status),
                _ => query.OrderByDescending(t => t.CreatedDate)
            };

            var items = await query
                .Include(t => t.Serial)
                    .ThenInclude(s => s.Variant)
                        .ThenInclude(v => v.Product)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task AddAsync(ServiceTicket ticket)
        {
            await _dbContext.ServiceTickets.AddAsync(ticket);
        }

        public async Task AddStatusHistoryAsync(ServiceTicketStatusHistory history)
        {
            await _dbContext.ServiceTicketStatusHistories.AddAsync(history);
        }

        public async Task SaveChangesAsync()
        {
            await _dbContext.SaveChangesAsync();
        }
    }
}
