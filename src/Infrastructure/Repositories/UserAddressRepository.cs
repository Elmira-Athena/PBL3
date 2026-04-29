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
    public class UserAddressRepository : IUserAddressRepository
    {
        private readonly HushStoreDbContext _context;

        public UserAddressRepository(HushStoreDbContext context)
        {
            _context = context;
        }

        public async Task<UserAddress?> GetByIdAsync(int id)
        {
            return await _context.UserAddresses
                .FirstOrDefaultAsync(ua => ua.Id == id);
        }

        public async Task<List<UserAddress>> GetByUserIdAsync(Guid userId)
        {
            return await _context.UserAddresses
                .Where(ua => ua.UserId == userId)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task AddAsync(UserAddress address)
        {
            await _context.UserAddresses.AddAsync(address);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
