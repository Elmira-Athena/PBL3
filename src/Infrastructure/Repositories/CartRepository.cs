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
                .Include(c => c.Variant)
                    .ThenInclude(v => v.Images)
                .Where(c => c.UserId == userId)
                .AsNoTracking()
                .OrderByDescending(c => c.CreatedDate)
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

        public async Task<Cart?> GetCartItemAsync(int cartItemId, Guid userId)
        {
            return await _context.Carts
                .FirstOrDefaultAsync(c => c.Id == cartItemId && c.UserId == userId);
        }

        public async Task<Cart?> FindByUserAndVariantAsync(Guid userId, int variantId)
        {
            return await _context.Carts
                .FirstOrDefaultAsync(c => c.UserId == userId && c.VariantId == variantId);
        }

        public async Task AddAsync(Cart cart)
        {
            await _context.Carts.AddAsync(cart);
        }

        public void Remove(Cart cart)
        {
            _context.Carts.Remove(cart);
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
