using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PBL3.Core.Entities;
using PBL3.Infrastructure.Data;
using PBL3.Shared.DTOs.Categories;
using PBL3.Shared.DTOs.Common;

namespace PBL3.Service.Categories
{
    public class CategoryService : ICategoryService
    {
        private readonly HushStoreDbContext _context;
        private readonly ILogger<CategoryService> _logger;

        public CategoryService(HushStoreDbContext context, ILogger<CategoryService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ========================================================
        // GET TREE — Trả về toàn bộ cây danh mục (recursive DTO)
        // ========================================================
        public async Task<ApiResult<List<CategoryTreeDto>>> GetTreeAsync()
        {
            // Lấy tất cả categories active (AsNoTracking cho read-only)
            var allCategories = await _context.Categories
                .AsNoTracking()
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();

            // Build tree in-memory từ flat list
            var treeDtos = BuildTree(allCategories, parentId: null);

            return ApiResult<List<CategoryTreeDto>>.Ok(treeDtos);
        }

        // ========================================================
        // GET BY ID — Chi tiết 1 danh mục
        // ========================================================
        public async Task<ApiResult<CategoryDto>> GetByIdAsync(int id)
        {
            var category = await _context.Categories
                .AsNoTracking()
                .Include(c => c.Parent)
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

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
            // Rule: Unique Name Per Level (cùng ParentId)
            var isDuplicate = await _context.Categories
                .AnyAsync(c => c.ParentId == request.ParentId
                            && c.Name == request.Name
                            && !c.IsDeleted);

            if (isDuplicate)
                return ApiResult<CategoryDto>.Fail("Tên danh mục đã tồn tại trong cấp này.");

            // Kiểm tra Slug unique
            var isSlugDuplicate = await _context.Categories
                .AnyAsync(c => c.Slug == request.Slug && !c.IsDeleted);

            if (isSlugDuplicate)
                return ApiResult<CategoryDto>.Fail("Slug đã tồn tại. Vui lòng chọn slug khác.");

            // Tính Level tự động
            int level = 0;
            if (request.ParentId.HasValue)
            {
                var parent = await _context.Categories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == request.ParentId.Value && !c.IsDeleted);

                if (parent == null)
                    return ApiResult<CategoryDto>.Fail("Danh mục cha không tồn tại.");

                level = parent.Level + 1;
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

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Tạo danh mục mới: {CategoryName} (Id: {CategoryId})", category.Name, category.Id);

            // Load lại kèm Parent để map DTO
            await _context.Entry(category).Reference(c => c.Parent).LoadAsync();
            var dto = MapToDto(category);

            return ApiResult<CategoryDto>.Ok(dto, "Tạo danh mục thành công.");
        }

        // ========================================================
        // UPDATE — Cập nhật danh mục (Circular Reference Check)
        // ========================================================
        public async Task<ApiResult<CategoryDto>> UpdateAsync(int id, UpdateCategoryRequest request)
        {
            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

            if (category == null)
                return ApiResult<CategoryDto>.Fail("Không tìm thấy danh mục yêu cầu.");

            // Rule: Unique Name Per Level (cùng ParentId, trừ chính nó)
            var isDuplicate = await _context.Categories
                .AnyAsync(c => c.ParentId == request.ParentId
                            && c.Name == request.Name
                            && c.Id != id
                            && !c.IsDeleted);

            if (isDuplicate)
                return ApiResult<CategoryDto>.Fail("Tên danh mục đã tồn tại trong cấp này.");

            // Kiểm tra Slug unique (trừ chính nó)
            var isSlugDuplicate = await _context.Categories
                .AnyAsync(c => c.Slug == request.Slug && c.Id != id && !c.IsDeleted);

            if (isSlugDuplicate)
                return ApiResult<CategoryDto>.Fail("Slug đã tồn tại. Vui lòng chọn slug khác.");

            // =====================================================
            // CIRCULAR REFERENCE CHECK (Thuật toán quan trọng)
            // =====================================================
            // Khi thay đổi ParentId, phải đảm bảo:
            //   1. Không đặt chính mình làm cha (self-reference)
            //   2. Không đặt con cháu của mình làm cha (circular)
            //
            // Thuật toán: Load tất cả categories vào Dictionary.
            //   Đi từ ParentId mới → duyệt ngược lên tổ tiên.
            //   Nếu gặp Id == category đang sửa → phát hiện vòng lặp.
            // =====================================================
            if (request.ParentId.HasValue)
            {
                if (request.ParentId.Value == id)
                    return ApiResult<CategoryDto>.Fail("Lỗi tham chiếu vòng: Không thể chọn chính mình làm danh mục cha.");

                var isCircular = await DetectCircularReferenceAsync(id, request.ParentId.Value);
                if (isCircular)
                    return ApiResult<CategoryDto>.Fail("Lỗi tham chiếu vòng: Không thể chọn cấp dưới làm cha của cấp trên.");
            }

            // Tính Level mới
            int newLevel = 0;
            if (request.ParentId.HasValue)
            {
                var parent = await _context.Categories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == request.ParentId.Value && !c.IsDeleted);

                if (parent == null)
                    return ApiResult<CategoryDto>.Fail("Danh mục cha không tồn tại.");

                newLevel = parent.Level + 1;
            }

            // Nếu ParentId thay đổi → cần cập nhật Level cho cả subtree
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

            await _context.SaveChangesAsync();

            _logger.LogInformation("Cập nhật danh mục: {CategoryName} (Id: {CategoryId})", category.Name, category.Id);

            // Load lại Parent để map DTO
            await _context.Entry(category).Reference(c => c.Parent).LoadAsync();
            var dto = MapToDto(category);

            return ApiResult<CategoryDto>.Ok(dto, "Cập nhật danh mục thành công.");
        }

        // ========================================================
        // DELETE — Xóa mềm danh mục (Soft Delete)
        // ========================================================
        public async Task<ApiResult<bool>> DeleteAsync(int id)
        {
            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

            if (category == null)
                return ApiResult<bool>.Fail("Không tìm thấy danh mục yêu cầu.");

            // Rule: Không cho xóa nếu có danh mục con đang hoạt động
            var hasActiveChildren = await _context.Categories
                .AnyAsync(c => c.ParentId == id && !c.IsDeleted);

            if (hasActiveChildren)
                return ApiResult<bool>.Fail("Không thể xóa danh mục đang có danh mục con hoặc sản phẩm.");

            // Rule: Không cho xóa nếu đang chứa sản phẩm
            var hasProducts = await _context.Products
                .AnyAsync(p => p.CategoryId == id && !p.IsDeleted);

            if (hasProducts)
                return ApiResult<bool>.Fail("Không thể xóa danh mục đang có danh mục con hoặc sản phẩm.");

            // Soft Delete
            category.IsDeleted = true;
            category.DeletedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Xóa mềm danh mục: {CategoryName} (Id: {CategoryId})", category.Name, category.Id);

            return ApiResult<bool>.Ok(true, "Xóa danh mục thành công.");
        }

        // ========================================================
        // PRIVATE HELPERS
        // ========================================================

        /// <summary>
        /// Thuật toán phát hiện tham chiếu vòng (Circular Reference).
        /// Duyệt từ newParentId → đi ngược lên tổ tiên.
        /// Nếu gặp categoryId → phát hiện vòng lặp → return true.
        /// </summary>
        private async Task<bool> DetectCircularReferenceAsync(int categoryId, int newParentId)
        {
            // Load tất cả categories vào Dictionary để duyệt nhanh (tránh N+1)
            var allCategories = await _context.Categories
                .AsNoTracking()
                .Where(c => !c.IsDeleted)
                .Select(c => new { c.Id, c.ParentId })
                .ToDictionaryAsync(c => c.Id, c => c.ParentId);

            // Duyệt ngược từ newParentId → Root
            var currentId = newParentId;
            var visited = new HashSet<int>(); // Đề phòng data lỗi gây infinite loop

            while (allCategories.ContainsKey(currentId))
            {
                // Nếu gặp categoryId → circular!
                if (currentId == categoryId)
                    return true;

                // Đánh dấu đã visit (phòng data bẩn)
                if (!visited.Add(currentId))
                    return true; // Dữ liệu lỗi: đã có vòng lặp trong DB

                var parentId = allCategories[currentId];

                // Đã đến Root (ParentId == null) → không có circular
                if (!parentId.HasValue)
                    return false;

                currentId = parentId.Value;
            }

            return false;
        }

        /// <summary>
        /// Cập nhật Level cho toàn bộ subtree khi ParentId thay đổi.
        /// </summary>
        private async Task UpdateSubtreeLevelsAsync(int parentId, int parentLevel)
        {
            var children = await _context.Categories
                .Where(c => c.ParentId == parentId && !c.IsDeleted)
                .ToListAsync();

            foreach (var child in children)
            {
                child.Level = parentLevel + 1;
                // Đệ quy xuống các cấp con
                await UpdateSubtreeLevelsAsync(child.Id, child.Level);
            }
        }

        /// <summary>
        /// Build cây danh mục từ flat list (in-memory).
        /// </summary>
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

        /// <summary>
        /// Manual mapping: Entity → CategoryDto.
        /// </summary>
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
