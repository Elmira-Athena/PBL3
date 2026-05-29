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
    public class EmployeeRepository(HushStoreDbContext context) : IEmployeeRepository
    {
        private readonly HushStoreDbContext _context =
            context ?? throw new ArgumentNullException(nameof(context));

        public async Task<(List<AppUser> Items, int TotalCount)> GetPagedListAsync(
            string? keyword,
            bool? isActive,
            byte? gender,
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending)
        {
            var query = _context.Users
                .Include(u => u.Profile)
                .Where(u => u.Type == 1)
                .AsNoTracking()
                .AsQueryable();

            if (isActive.HasValue)
                query = query.Where(u => u.IsActive == isActive.Value);

            if (gender.HasValue)
                query = query.Where(u => u.Profile != null && u.Profile.Gender == gender.Value);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var lower = keyword.ToLower();
                query = query.Where(u =>
                    (u.Email != null && u.Email.ToLower().Contains(lower)) ||
                    (u.PhoneNumber != null && u.PhoneNumber.Contains(lower)) ||
                    (u.Profile != null && u.Profile.FullName.ToLower().Contains(lower)));
            }

            if (!string.IsNullOrWhiteSpace(sortBy))
            {
                query = sortBy.ToLower() switch
                {
                    "fullname" => sortDescending
                        ? query.OrderByDescending(u => u.Profile!.FullName)
                        : query.OrderBy(u => u.Profile!.FullName),
                    "email" => sortDescending
                        ? query.OrderByDescending(u => u.Email)
                        : query.OrderBy(u => u.Email),
                    "phonenumber" => sortDescending
                        ? query.OrderByDescending(u => u.PhoneNumber)
                        : query.OrderBy(u => u.PhoneNumber),
                    _ => sortDescending
                        ? query.OrderByDescending(u => u.CreatedDate)
                        : query.OrderBy(u => u.CreatedDate)
                };
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
                .Where(u => u.Type == 1)
                .FirstOrDefaultAsync(u => u.Id == id);
        }
    }
}
