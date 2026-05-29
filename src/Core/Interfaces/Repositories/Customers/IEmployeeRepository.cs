using PBL3.Core.Entities;

namespace PBL3.Core.Interfaces
{
    /// <summary>
    /// Repository interface cho Employee (Type = 1).
    /// </summary>
    public interface IEmployeeRepository
    {
        Task<(List<AppUser> Items, int TotalCount)> GetPagedListAsync(
            string? keyword,
            bool? isActive,
            byte? gender,
            int pageNumber,
            int pageSize,
            string? sortBy,
            bool sortDescending);

        Task<AppUser?> GetByIdWithProfileAsync(Guid id);
    }
}
