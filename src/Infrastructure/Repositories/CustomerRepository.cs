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
    public class CustomerRepository : ICustomerRepository
    {
        private readonly HushStoreDbContext _context;

        public CustomerRepository(HushStoreDbContext context)
        {
            _context = context;
        }

        public async Task<(List<AppUser> Items, int TotalCount)> GetPagedListAsync(
            string? keyword,
            bool? isActive,
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending)
        {
            // Join AppUser and UserProfile to filter and sort efficiently
            // Only customers (Type = 2)
            var query = _context.Users
                .Include(u => u.Profile)
                .Where(u => u.Type == 2)
                .AsNoTracking()
                .AsQueryable();

            // Lọc theo IsActive nếu có
            if (isActive.HasValue)
            {
                query = query.Where(u => u.IsActive == isActive.Value);
            }

            // Lọc theo keyword (tìm trên Email, PhoneNumber, FullName)
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var lowerKeyword = keyword.ToLower();
                query = query.Where(u => 
                    (u.Email != null && u.Email.ToLower().Contains(lowerKeyword)) ||
                    (u.PhoneNumber != null && u.PhoneNumber.Contains(lowerKeyword)) ||
                    (u.Profile != null && u.Profile.FullName.ToLower().Contains(lowerKeyword)));
            }

            // Sắp xếp
            if (!string.IsNullOrWhiteSpace(sortBy))
            {
                switch (sortBy.ToLower())
                {
                    case "fullname":
                        query = sortDescending 
                            ? query.OrderByDescending(u => u.Profile!.FullName) 
                            : query.OrderBy(u => u.Profile!.FullName);
                        break;
                    case "email":
                        query = sortDescending 
                            ? query.OrderByDescending(u => u.Email) 
                            : query.OrderBy(u => u.Email);
                        break;
                    case "phonenumber":
                        query = sortDescending 
                            ? query.OrderByDescending(u => u.PhoneNumber) 
                            : query.OrderBy(u => u.PhoneNumber);
                        break;
                    case "createddate":
                    default:
                        query = sortDescending 
                            ? query.OrderByDescending(u => u.CreatedDate) 
                            : query.OrderBy(u => u.CreatedDate);
                        break;
                }
            }
            else
            {
                query = query.OrderByDescending(u => u.CreatedDate);
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<AppUser?> GetByIdWithProfileAsync(Guid id)
        {
            return await _context.Users
                .Include(u => u.Profile)
                .Where(u => u.Type == 2) // Guarantee customer only
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<List<Order>> GetRecentOrdersAsync(Guid userId, int count)
        {
            return await _context.Orders
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .Take(count)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<bool> HasPendingOrdersAsync(Guid userId)
        {
            // Statuses: 0 = Pending, 1 = Confirmed, 2 = Shipping
            return await _context.Orders
                .AnyAsync(o => o.UserId == userId && (o.Status == 0 || o.Status == 1 || o.Status == 2));
        }
    }
}
