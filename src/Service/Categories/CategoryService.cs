using Microsoft.Extensions.Logging;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Shared.DTOs.Categories;
using PBL3.Shared.DTOs.Common;

namespace PBL3.Service.Categories
{
    public class CategoryService(ICategoryRepository categoryRepo, ILogger<CategoryService> logger) : ICategoryService
    {
        private readonly ICategoryRepository _categoryRepo = categoryRepo;
        private readonly ILogger<CategoryService> _logger = logger;

        // ========================================================
        // GET TREE — Trả về toàn bộ cây danh mục (recursive DTO)
        // ========================================================
        public async Task<ApiResult<List<CategoryTreeDto>>> GetTreeAsync()
        {
            var allCategories = await _categoryRepo.GetAllActiveAsync();
            var treeDtos = BuildTree(allCategories, parentId: null);
            return ApiResult<List<CategoryTreeDto>>.Ok(treeDtos);
        }

        // ========================================================
        // GET BY ID — Chi tiết 1 danh mục
        // ========================================================
        public async Task<ApiResult<CategoryDto>> GetByIdAsync(int id)
        {
            var category = await _categoryRepo.GetByIdAsync(id, includeParent: true);

            if (category == null)
                return ApiResult<CategoryDto>.Fail("Không tìm thấy danh mục yêu cầu.");

            var dto = MapToDto(category);
            return ApiResult<CategoryDto>.Ok(dto);
        }

        // ========================================================
        // CREATE — Tạo mới danh mục
        // ========================================================
        public async Task<ApiResult<CategoryDto>> CreateAsync(CreateCategoryRequest request)
        {
            // Tính Level tự động
            int level = 0;
            if (request.ParentId.HasValue)
            {
                var parent = await _categoryRepo.GetByIdAsync(request.ParentId.Value);
                // Note: Parent null check handled by Validator
                level = parent!.Level + 1;
            }

            var category = new Category
            {
                Name = request.Name,
                Slug = request.Slug,
                ParentId = request.ParentId,
                Level = level,
                ImageUrl = request.ImageUrl,
                SortOrder = request.SortOrder,
                IsVisible = request.IsVisible,
                CreatedDate = DateTime.UtcNow
            };

            await _categoryRepo.AddAsync(category);
            await _categoryRepo.SaveChangesAsync();

            _logger.LogInformation("Tạo danh mục mới: {CategoryName} (Id: {CategoryId})", category.Name, category.Id);

            // Load lại kèm Parent để map DTO
            var created = await _categoryRepo.GetByIdAsync(category.Id, includeParent: true);
            var dto = MapToDto(created!);

            return ApiResult<CategoryDto>.Ok(dto, "Tạo danh mục thành công.");
        }

        // ========================================================
        // UPDATE — Cập nhật danh mục (Circular Reference Check)
        // ========================================================
        public async Task<ApiResult<CategoryDto>> UpdateAsync(int id, UpdateCategoryRequest request)
        {
            var category = await _categoryRepo.GetByIdAsync(id);

            if (category == null)
                return ApiResult<CategoryDto>.Fail("Không tìm thấy danh mục yêu cầu.");

            // Note: Duplication and Circular Reference checks handled by Validator

            // Tính Level mới
            int newLevel = 0;
            if (request.ParentId.HasValue)
            {
                var parent = await _categoryRepo.GetByIdAsync(request.ParentId.Value);
                newLevel = parent!.Level + 1;
            }

            bool parentChanged = category.ParentId != request.ParentId;

            // Cập nhật entity
            category.Name = request.Name;
            category.Slug = request.Slug;
            category.ParentId = request.ParentId;
            category.Level = newLevel;
            category.ImageUrl = request.ImageUrl;
            category.SortOrder = request.SortOrder;
            category.IsVisible = request.IsVisible;
            category.ModifiedDate = DateTime.UtcNow;

            // Nếu ParentId thay đổi → cập nhật Level đệ quy cho toàn bộ subtree
            if (parentChanged)
            {
                await UpdateSubtreeLevelsAsync(category.Id, newLevel);
            }

            await _categoryRepo.SaveChangesAsync();

            _logger.LogInformation("Cập nhật danh mục: {CategoryName} (Id: {CategoryId})", category.Name, category.Id);

            var updated = await _categoryRepo.GetByIdAsync(category.Id, includeParent: true);
            var dto = MapToDto(updated!);

            return ApiResult<CategoryDto>.Ok(dto, "Cập nhật danh mục thành công.");
        }

        // ========================================================
        // DELETE — Xóa mềm danh mục (Soft Delete)
        // ========================================================
        public async Task<ApiResult<bool>> DeleteAsync(int id)
        {
            var category = await _categoryRepo.GetByIdAsync(id);

            if (category == null)
                return ApiResult<bool>.Fail("Không tìm thấy danh mục yêu cầu.");

            if (await _categoryRepo.HasActiveChildrenAsync(id))
                return ApiResult<bool>.Fail("Không thể xóa danh mục đang có danh mục con hoặc sản phẩm.");

            if (await _categoryRepo.HasProductsAsync(id))
                return ApiResult<bool>.Fail("Không thể xóa danh mục đang có danh mục con hoặc sản phẩm.");

            category.IsDeleted = true;
            category.DeletedDate = DateTime.UtcNow;

            await _categoryRepo.SaveChangesAsync();

            _logger.LogInformation("Xóa mềm danh mục: {CategoryName} (Id: {CategoryId})", category.Name, category.Id);

            return ApiResult<bool>.Ok(true, "Xóa danh mục thành công.");
        }

        // ========================================================
        // PRIVATE HELPERS
        // ========================================================

        private async Task<bool> DetectCircularReferenceAsync(int categoryId, int newParentId)
        {
            var allCategories = await _categoryRepo.GetAllCategoryParentMapAsync();

            var currentId = newParentId;
            var visited = new HashSet<int>();

            while (allCategories.ContainsKey(currentId))
            {
                if (currentId == categoryId)
                    return true;

                if (!visited.Add(currentId))
                    return true;

                var parentId = allCategories[currentId];

                if (!parentId.HasValue)
                    return false;

                currentId = parentId.Value;
            }

            return false;
        }

        private async Task UpdateSubtreeLevelsAsync(int parentId, int parentLevel)
        {
            var children = await _categoryRepo.GetChildrenAsync(parentId);

            foreach (var child in children)
            {
                child.Level = parentLevel + 1;
                await UpdateSubtreeLevelsAsync(child.Id, child.Level);
            }
        }

        private List<CategoryTreeDto> BuildTree(List<Category> allCategories, int? parentId)
        {
            return allCategories
                .Where(c => c.ParentId == parentId)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .Select(c => new CategoryTreeDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Slug = c.Slug,
                    ParentId = c.ParentId,
                    Level = c.Level,
                    ImageUrl = c.ImageUrl,
                    SortOrder = c.SortOrder,
                    IsVisible = c.IsVisible,
                    Children = BuildTree(allCategories, c.Id)
                })
                .ToList();
        }

        private static CategoryDto MapToDto(Category entity)
        {
            return new CategoryDto
            {
                Id = entity.Id,
                Name = entity.Name,
                Slug = entity.Slug,
                ParentId = entity.ParentId,
                ParentName = entity.Parent?.Name,
                Level = entity.Level,
                ImageUrl = entity.ImageUrl,
                SortOrder = entity.SortOrder,
                IsVisible = entity.IsVisible,
                CreatedDate = entity.CreatedDate
            };
        }
    }
}
