using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;

namespace PBL3.Infrastructure.Repositories
{
    public class ProductReviewRepository : IProductReviewRepository
    {
        private readonly HushStoreDbContext _context;

        public ProductReviewRepository(HushStoreDbContext context) => _context = context;

        public async Task<(List<ProductReview> Items, int TotalCount)> GetPagedByProductIdAsync(
            int productId, int pageNumber, int pageSize)
        {
            var query = _context.ProductReviews
                .AsNoTracking()
                .Where(r => r.ProductId == productId)
                .Include(r => r.User).ThenInclude(u => u.Profile);

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(r => r.CreatedDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public Task<bool> ExistsAsync(int productId, Guid userId) =>
            _context.ProductReviews.AnyAsync(r => r.ProductId == productId && r.UserId == userId);

        public Task<ProductReview?> GetByIdAsync(int id) =>
            _context.ProductReviews.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);

        public Task<ProductReview?> GetByIdWithTrackingAsync(int id) =>
            _context.ProductReviews.FirstOrDefaultAsync(r => r.Id == id);

        public async Task AddAsync(ProductReview review) =>
            await _context.ProductReviews.AddAsync(review);

        public Task SaveChangesAsync() => _context.SaveChangesAsync();
    }
}
