using PBL3.Core.Entities;

namespace PBL3.Core.Interfaces
{
    /// <summary>
    /// Repository interface cho Category.
    /// </summary>
    public interface ICategoryRepository
    {
        Task<List<Category>> GetAllActiveAsync();
        Task<Category?> GetByIdAsync(int id, bool includeParent = false);
        Task<bool> IsDuplicateNameAsync(int? parentId, string name, int? excludeId = null);
        Task<bool> IsDuplicateSlugAsync(string slug, int? excludeId = null);
        Task<bool> HasActiveChildrenAsync(int id);
        Task<bool> HasProductsAsync(int id);
        Task<Dictionary<int, int?>> GetAllCategoryParentMapAsync();
        Task<List<Category>> GetChildrenAsync(int parentId);
        Task AddAsync(Category category);
        Task SaveChangesAsync();
    }
}
