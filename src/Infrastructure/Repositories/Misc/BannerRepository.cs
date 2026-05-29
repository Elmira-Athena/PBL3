using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;

namespace PBL3.Infrastructure.Repositories
{
    public class BannerRepository : IBannerRepository
    {
        private readonly HushStoreDbContext _context;

        public BannerRepository(HushStoreDbContext context)
        {
            _context = context;
        }

        public async Task<(List<Banner> Items, int TotalCount)> GetPagedListAsync(
            string? keyword,
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending)
        {
            var query = _context.Banners.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(b => b.Title.ToLower().Contains(kw));
            }

            var totalCount = await query.CountAsync();

            query = sortBy?.ToLower() switch
            {
                "title"       => sortDescending ? query.OrderByDescending(b => b.Title)       : query.OrderBy(b => b.Title),
                "sortorder"   => sortDescending ? query.OrderByDescending(b => b.SortOrder)   : query.OrderBy(b => b.SortOrder),
                "createddate" => sortDescending ? query.OrderByDescending(b => b.CreatedDate) : query.OrderBy(b => b.CreatedDate),
                _             => query.OrderBy(b => b.SortOrder).ThenBy(b => b.Id)
            };

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<Banner?> GetByIdAsync(int id)
        {
            return await _context.Banners
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == id);
        }

        public async Task<Banner?> GetByIdWithTrackingAsync(int id)
        {
            return await _context.Banners
                .FirstOrDefaultAsync(b => b.Id == id);
        }

        public async Task<List<Banner>> GetActiveAsync(DateTime nowUtc)
        {
            return await _context.Banners
                .AsNoTracking()
                .Where(b => b.IsActive
                            && (b.StartDate == null || b.StartDate <= nowUtc)
                            && (b.EndDate == null || b.EndDate >= nowUtc))
                .OrderBy(b => b.SortOrder)
                .ThenBy(b => b.Id)
                .ToListAsync();
        }

        public async Task AddAsync(Banner banner)
        {
            _context.Banners.Add(banner);
            await Task.CompletedTask;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
