using PBL3.Shared.DTOs.Categories;
using PBL3.Shared.DTOs.Common;

namespace Client.Services.Category
{
    public interface ICategoryClientService
    {
        Task<ApiResult<List<CategoryTreeDto>>> GetTreeAsync();
        Task<ApiResult<CategoryDto>> GetByIdAsync(int id);
        Task<ApiResult<CategoryDto>> CreateAsync(CreateCategoryRequest request);
        Task<ApiResult<CategoryDto>> UpdateAsync(int id, UpdateCategoryRequest request);
        Task<ApiResult<bool>> DeleteAsync(int id);
    }
}
