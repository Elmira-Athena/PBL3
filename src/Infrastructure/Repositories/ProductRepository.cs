using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;

namespace PBL3.Infrastructure.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly HushStoreDbContext _context;

        public ProductRepository(HushStoreDbContext context)
        {
            _context = context;
        }

        public async Task<Product?> GetByIdAsync(int id)
        {
            return await _context.Products
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        }

        public async Task<Product?> GetByIdWithDetailsAsync(int id)
        {
            return await _context.Products
                .Include(p => p.Manufacturer)
                .Include(p => p.Category)
                .Include(p => p.Variants.Where(v => !v.IsDeleted))
                    .ThenInclude(v => v.Images)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        }

        public async Task<bool> IsSkuExistsAsync(string sku, int? excludeVariantId = null)
        {
            var query = _context.ProductVariants
                .Where(v => v.SKU == sku && !v.IsDeleted);

            if (excludeVariantId.HasValue)
                query = query.Where(v => v.Id != excludeVariantId.Value);

            return await query.AnyAsync();
        }

        public async Task<bool> ManufacturerExistsAsync(int manufacturerId)
        {
            return await _context.Manufacturers
                .AnyAsync(m => m.Id == manufacturerId && !m.IsDeleted);
        }

        public async Task<bool> CategoryExistsAsync(int categoryId)
        {
            return await _context.Categories
                .AnyAsync(c => c.Id == categoryId && !c.IsDeleted);
        }

        public async Task<List<int>> GetCategoryChildIdsAsync(int categoryId)
        {
            // Lấy tất cả category con (đệ quy) để hỗ trợ filter
            var allCategories = await _context.Categories
                .AsNoTracking()
                .Where(c => !c.IsDeleted)
                .Select(c => new { c.Id, c.ParentId })
                .ToListAsync();

            var result = new List<int> { categoryId };
            var queue = new Queue<int>();
            queue.Enqueue(categoryId);

            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();
                var children = allCategories
                    .Where(c => c.ParentId == currentId)
                    .Select(c => c.Id)
                    .ToList();

                foreach (var childId in children)
                {
                    result.Add(childId);
                    queue.Enqueue(childId);
                }
            }

            return result;
        }

        public async Task<(List<Product> Items, int TotalCount)> GetPagedListAsync(
            string? keyword,
            List<int>? categoryIds,
            int? manufacturerId,
            decimal? priceMin,
            decimal? priceMax,
            int? status,
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending)
        {
            var query = _context.Products
                .AsNoTracking()
                .Include(p => p.Manufacturer)
                .Include(p => p.Category)
                .Include(p => p.Variants.Where(v => !v.IsDeleted))
                    .ThenInclude(v => v.Images)
                .Where(p => !p.IsDeleted);

            // Filter: Keyword (tìm theo tên sản phẩm)
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(p =>
                    p.Name.ToLower().Contains(kw) ||
                    (p.ShortDescription != null && p.ShortDescription.ToLower().Contains(kw)) ||
                    p.Variants.Any(v => v.SKU.ToLower().Contains(kw)));
            }

            // Filter: Category (bao gồm category con)
            if (categoryIds != null && categoryIds.Count > 0)
            {
                query = query.Where(p => categoryIds.Contains(p.CategoryId));
            }

            // Filter: Manufacturer
            if (manufacturerId.HasValue)
            {
                query = query.Where(p => p.ManufacturerId == manufacturerId.Value);
            }

            // Filter: Price range (dựa trên giá Variant)
            if (priceMin.HasValue)
            {
                query = query.Where(p => p.Variants.Any(v => v.Price >= priceMin.Value));
            }
            if (priceMax.HasValue)
            {
                query = query.Where(p => p.Variants.Any(v => v.Price <= priceMax.Value));
            }

            // Filter: Status
            if (status.HasValue)
            {
                query = query.Where(p => p.Status == status.Value);
            }

            // Total count (before paging)
            var totalCount = await query.CountAsync();

            // Sorting
            query = sortBy?.ToLower() switch
            {
                "name" => sortDescending
                    ? query.OrderByDescending(p => p.Name)
                    : query.OrderBy(p => p.Name),
                "price" => sortDescending
                    ? query.OrderByDescending(p => p.Variants.Min(v => v.Price))
                    : query.OrderBy(p => p.Variants.Min(v => v.Price)),
                "created" => sortDescending
                    ? query.OrderByDescending(p => p.CreatedDate)
                    : query.OrderBy(p => p.CreatedDate),
                _ => query.OrderByDescending(p => p.CreatedDate) // Default: mới nhất
            };

            // Paging
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task AddAsync(Product product)
        {
            await _context.Products.AddAsync(product);
        }

        public async Task AddVariantAsync(ProductVariant variant)
        {
            await _context.ProductVariants.AddAsync(variant);
        }

        public void RemoveVariant(ProductVariant variant)
        {
            _context.ProductVariants.Remove(variant);
        }

        // Fix: interface khai báo Task RemoveVariant nhưng implementation là void
        // Sửa lại cho khớp
        Task IProductRepository.RemoveVariant(ProductVariant variant)
        {
            _context.ProductVariants.Remove(variant);
            return Task.CompletedTask;
        }

        public async Task<List<int>> GetExistingVariantIdsAsync(List<int> variantIds)
        {
            return await _context.ProductVariants
                .AsNoTracking()
                .Where(v => variantIds.Contains(v.Id) && !v.IsDeleted)
                .Select(v => v.Id)
                .ToListAsync();
        }

        public async Task<List<ProductVariant>> FilterBySpecificationAsync(string specKey, string specValue)
        {
            string jsonPath = $"$.{specKey}";
            return await _context.ProductVariants
                .FromSqlInterpolated($"SELECT * FROM ProductVariants WHERE JSON_VALUE(Specifications, {jsonPath}) = {specValue}")
                .AsNoTracking()
                .Where(v => !v.IsDeleted)
                .Include(v => v.Product)
                .Include(v => v.Images)
                .ToListAsync();
        }

        public async Task<ProductVariant?> GetVariantByIdAsync(int variantId)
        {
            return await _context.ProductVariants
                .FirstOrDefaultAsync(v => v.Id == variantId && !v.IsDeleted);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
