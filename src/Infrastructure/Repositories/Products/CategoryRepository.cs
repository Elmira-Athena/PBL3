using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;

namespace PBL3.Infrastructure.Repositories
{
    public class CategoryRepository(HushStoreDbContext context) : ICategoryRepository
    {
        private readonly HushStoreDbContext _context =
            context ?? throw new ArgumentNullException(nameof(context));

        public async Task<List<Category>> GetAllActiveAsync()
        {
            return await _context.Categories
                .AsNoTracking()
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<Category?> GetByIdAsync(int id, bool includeParent = false)
        {
            var query = _context.Categories.AsQueryable();

            if (includeParent)
                query = query.Include(c => c.Parent);

            return await query.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        }

        public async Task<bool> IsDuplicateNameAsync(int? parentId, string name, int? excludeId = null)
        {
            var query = _context.Categories
                .Where(c => c.ParentId == parentId && c.Name == name && !c.IsDeleted);

            if (excludeId.HasValue)
                query = query.Where(c => c.Id != excludeId.Value);

            return await query.AnyAsync();
        }

        public async Task<bool> IsDuplicateSlugAsync(string slug, int? excludeId = null)
        {
            var query = _context.Categories
                .Where(c => c.Slug == slug && !c.IsDeleted);

            if (excludeId.HasValue)
                query = query.Where(c => c.Id != excludeId.Value);

            return await query.AnyAsync();
        }

        public async Task<bool> HasActiveChildrenAsync(int id)
        {
            return await _context.Categories
                .AnyAsync(c => c.ParentId == id && !c.IsDeleted);
        }

        public async Task<bool> HasProductsAsync(int id)
        {
            return await _context.Products
                .AnyAsync(p => p.CategoryId == id && !p.IsDeleted);
        }

        public async Task<Dictionary<int, int?>> GetAllCategoryParentMapAsync()
        {
            return await _context.Categories
                .AsNoTracking()
                .Where(c => !c.IsDeleted)
                .Select(c => new { c.Id, c.ParentId })
                .ToDictionaryAsync(c => c.Id, c => c.ParentId);
        }

        public async Task<List<Category>> GetChildrenAsync(int parentId)
        {
            return await _context.Categories
                .Where(c => c.ParentId == parentId && !c.IsDeleted)
                .ToListAsync();
        }

        public async Task AddAsync(Category category)
        {
            _context.Categories.Add(category);
            await Task.CompletedTask;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
