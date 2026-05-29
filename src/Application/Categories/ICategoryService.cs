using PBL3.Shared.DTOs.Categories;
using PBL3.Shared.DTOs.Common;

namespace PBL3.Application.Categories
{
    public interface ICategoryService
    {
        /// <summary>
        /// Lấy toàn bộ cây danh mục (recursive).
        /// </summary>
        Task<ApiResult<List<CategoryTreeDto>>> GetTreeAsync();

        /// <summary>
        /// Lấy chi tiết 1 danh mục theo Id.
        /// </summary>
        Task<ApiResult<CategoryDto>> GetByIdAsync(int id);

        /// <summary>
        /// Tạo mới danh mục.
        /// </summary>
        Task<ApiResult<CategoryDto>> CreateAsync(CreateCategoryRequest request);

        /// <summary>
        /// Cập nhật danh mục (bao gồm Circular Reference Check).
        /// </summary>
        Task<ApiResult<CategoryDto>> UpdateAsync(int id, UpdateCategoryRequest request);

        /// <summary>
        /// Xóa mềm danh mục (Soft Delete).
        /// </summary>
        Task<ApiResult<bool>> DeleteAsync(int id);
    }
}
