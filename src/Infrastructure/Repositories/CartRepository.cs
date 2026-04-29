using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PBL3.Infrastructure.Repositories
{
    public class CartRepository : ICartRepository
    {
        private readonly HushStoreDbContext _context;

        public CartRepository(HushStoreDbContext context)
        {
            _context = context;
        }

        public async Task<List<Cart>> GetCartItemsByUserAsync(Guid userId)
        {
            return await _context.Carts
                .Include(c => c.Variant)
                    .ThenInclude(v => v.Product)
                .Where(c => c.UserId == userId)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<Cart>> GetCartItemsWithTrackingAsync(Guid userId)
        {
            return await _context.Carts
                .Include(c => c.Variant)
                    .ThenInclude(v => v.Product)
                .Where(c => c.UserId == userId)
                .ToListAsync();
        }

        public void RemoveRange(IEnumerable<Cart> carts)
        {
            _context.Carts.RemoveRange(carts);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
