using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;

namespace PBL3.Infrastructure.Repositories
{
    public class OrderRepository : IOrderRepository
    {
        private readonly HushStoreDbContext _dbContext;

        public OrderRepository(HushStoreDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Order?> GetByIdWithDetailsAsync(int id)
        {
            return await _dbContext.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Variant)
                .Include(o => o.VoucherUsages)
                    .ThenInclude(vu => vu.Voucher)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        public async Task<string?> GetLastOrderCodeByDateAsync(string datePrefix)
        {
            return await _dbContext.Orders
                .Where(o => o.OrderCode.StartsWith(datePrefix))
                .OrderByDescending(o => o.OrderCode)
                .Select(o => o.OrderCode)
                .FirstOrDefaultAsync();
        }

        public async Task AddAsync(Order order)
        {
            await _dbContext.Orders.AddAsync(order);
        }

        public async Task SaveChangesAsync()
        {
            await _dbContext.SaveChangesAsync();
        }
    }
}
