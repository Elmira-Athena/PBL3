using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;

namespace PBL3.Infrastructure.Repositories
{
    public class SupplierRepository(HushStoreDbContext context) : ISupplierRepository
    {
        private readonly HushStoreDbContext _context =
            context ?? throw new ArgumentNullException(nameof(context));

        public async Task<(List<Supplier> Items, int TotalCount)> GetPagedListAsync(
            string? keyword,
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending)
        {
            // Global Query Filter đã tự động lọc IsDeleted == true
            var query = _context.Suppliers.AsNoTracking().AsQueryable();

            // Tìm kiếm theo Tên hoặc Số điện thoại
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(s =>
                    s.Name.ToLower().Contains(kw) ||
                    s.PhoneNumber.Contains(kw));
            }

            // Đếm tổng trước khi phân trang
            var totalCount = await query.CountAsync();

            // Sắp xếp
            query = sortBy?.ToLower() switch
            {
                "name" => sortDescending ? query.OrderByDescending(s => s.Name) : query.OrderBy(s => s.Name),
                "phone" => sortDescending ? query.OrderByDescending(s => s.PhoneNumber) : query.OrderBy(s => s.PhoneNumber),
                "createddate" => sortDescending ? query.OrderByDescending(s => s.CreatedDate) : query.OrderBy(s => s.CreatedDate),
                _ => query.OrderByDescending(s => s.CreatedDate) // Mặc định: mới nhất trước
            };

            // Phân trang
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<Supplier?> GetByIdAsync(int id)
        {
            // Global Query Filter tự động loại IsDeleted
            return await _context.Suppliers
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<bool> HasImportReceiptsAsync(int supplierId)
        {
            return await _context.ImportReceipts
                .AnyAsync(r => r.SupplierId == supplierId);
        }

        public async Task AddAsync(Supplier supplier)
        {
            _context.Suppliers.Add(supplier);
            await Task.CompletedTask;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
