using PBL3.Core.Entities;

namespace PBL3.Core.Interfaces
{
    /// <summary>
    /// Repository interface cho UserAddress.
    /// </summary>
    public interface IUserAddressRepository
    {
        Task<UserAddress?> GetByIdAsync(int id);
        Task<List<UserAddress>> GetByUserIdAsync(Guid userId);
        Task AddAsync(UserAddress address);
        Task ClearUserDefaultsAsync(Guid userId);
        Task SaveChangesAsync();
    }
}
