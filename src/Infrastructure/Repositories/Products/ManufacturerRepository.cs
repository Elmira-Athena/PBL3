using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;

namespace PBL3.Infrastructure.Repositories
{
    public class ManufacturerRepository(HushStoreDbContext context) : IManufacturerRepository
    {
        private readonly HushStoreDbContext _context =
            context ?? throw new ArgumentNullException(nameof(context));

        // ========================================================
        // GET PAGED LIST — Phân trang + tìm kiếm
        // ========================================================
        public async Task<(List<Manufacturer> Items, int TotalCount)> GetPagedListAsync(
            string? keyword,
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending)
        {
            // Global Query Filter tự động lọc IsDeleted == true
            var query = _context.Manufacturers.AsNoTracking().AsQueryable();

            // Tìm kiếm theo Tên hoặc Website
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(m =>
                    m.Name.ToLower().Contains(kw) ||
                    (m.Website != null && m.Website.ToLower().Contains(kw)));
            }

            // Đếm tổng trước khi phân trang
            var totalCount = await query.CountAsync();

            // Sắp xếp
            query = sortBy?.ToLower() switch
            {
                "name"        => sortDescending ? query.OrderByDescending(m => m.Name) : query.OrderBy(m => m.Name),
                "createddate" => sortDescending ? query.OrderByDescending(m => m.CreatedDate) : query.OrderBy(m => m.CreatedDate),
                _             => query.OrderBy(m => m.Name) // Mặc định: A-Z theo tên
            };

            // Phân trang
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        // ========================================================
        // GET BY ID
        // ========================================================
        public async Task<Manufacturer?> GetByIdAsync(int id)
        {
            // Global Query Filter tự động loại IsDeleted
            return await _context.Manufacturers
                .FirstOrDefaultAsync(m => m.Id == id);
        }

        // ========================================================
        // GET ALL ACTIVE — Dùng cho Dropdown
        // ========================================================
        public async Task<List<Manufacturer>> GetAllActiveAsync()
        {
            return await _context.Manufacturers
                .AsNoTracking()
                .OrderBy(m => m.Name)
                .ToListAsync();
        }

        // ========================================================
        // HAS PRODUCTS — Kiểm tra ràng buộc xóa
        // ========================================================
        public async Task<bool> HasProductsAsync(int manufacturerId)
        {
            return await _context.Products
                .AnyAsync(p => p.ManufacturerId == manufacturerId && !p.IsDeleted);
        }

        // ========================================================
        // IS DUPLICATE NAME — Kiểm tra trùng tên
        // ========================================================
        public async Task<bool> IsDuplicateNameAsync(string name, int? excludeId = null)
        {
            var normalizedName = name.Trim().ToLower();
            return await _context.Manufacturers
                .AnyAsync(m =>
                    m.Name.ToLower() == normalizedName &&
                    (excludeId == null || m.Id != excludeId));
        }

        // ========================================================
        // ADD + SAVE
        // ========================================================
        public async Task AddAsync(Manufacturer manufacturer)
        {
            _context.Manufacturers.Add(manufacturer);
            await Task.CompletedTask;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
