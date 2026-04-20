using FluentValidation;
using PBL3.Core.Interfaces;
using PBL3.Shared.DTOs.Categories;

namespace PBL3.Service.Categories
{
    public class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
    {
        private readonly ICategoryRepository _categoryRepo;

        public CreateCategoryRequestValidator(ICategoryRepository categoryRepo)
        {
            _categoryRepo = categoryRepo;

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Tên danh mục không được để trống.")
                .MaximumLength(100).WithMessage("Tên danh mục không quá 100 ký tự.")
                .MustAsync(async (req, name, cancellation) => 
                    !await _categoryRepo.IsDuplicateNameAsync(req.ParentId, name))
                .WithMessage("Tên danh mục đã tồn tại trong cấp này.");

            RuleFor(x => x.Slug)
                .NotEmpty().WithMessage("Slug không được để trống.")
                .Matches(@"^[a-z0-9-]+$").WithMessage("Slug chỉ được chứa chữ thường, số và dấu gạch ngang.")
                .MustAsync(async (slug, cancellation) => 
                    !await _categoryRepo.IsDuplicateSlugAsync(slug))
                .WithMessage("Slug đã tồn tại. Vui lòng chọn slug khác.");

            RuleFor(x => x.ParentId)
                .MustAsync(async (parentId, cancellation) =>
                {
                    if (!parentId.HasValue) return true;
                    var parent = await _categoryRepo.GetByIdAsync(parentId.Value);
                    return parent != null;
                })
                .WithMessage("Danh mục cha không tồn tại.");
        }
    }

    public class UpdateCategoryRequestValidator : AbstractValidator<(int Id, UpdateCategoryRequest Request)>
    {
        private readonly ICategoryRepository _categoryRepo;

        public UpdateCategoryRequestValidator(ICategoryRepository categoryRepo)
        {
            _categoryRepo = categoryRepo;

            RuleFor(x => x.Request.Name)
                .NotEmpty().WithMessage("Tên danh mục không được để trống.")
                .MaximumLength(100).WithMessage("Tên danh mục không quá 100 ký tự.")
                .MustAsync(async (tuple, name, cancellation) => 
                    !await _categoryRepo.IsDuplicateNameAsync(tuple.Request.ParentId, name, excludeId: tuple.Id))
                .WithMessage("Tên danh mục đã tồn tại trong cấp này.");

            RuleFor(x => x.Request.Slug)
                .NotEmpty().WithMessage("Slug không được để trống.")
                .Matches(@"^[a-z0-9-]+$").WithMessage("Slug chỉ được chứa chữ thường, số và dấu gạch ngang.")
                .MustAsync(async (tuple, slug, cancellation) => 
                    !await _categoryRepo.IsDuplicateSlugAsync(slug, excludeId: tuple.Id))
                .WithMessage("Slug đã tồn tại. Vui lòng chọn slug khác.");

            RuleFor(x => x.Request.ParentId)
                .MustAsync(async (tuple, parentId, cancellation) =>
                {
                    if (!parentId.HasValue) return true;
                    if (parentId.Value == tuple.Id) return false;
                    var parent = await _categoryRepo.GetByIdAsync(parentId.Value);
                    return parent != null;
                })
                .WithMessage("Danh mục cha không tồn tại hoặc lỗi tham chiếu chính mình.")
                .MustAsync(async (tuple, parentId, cancellation) =>
                {
                    if (!parentId.HasValue) return true;
                    var isCircular = await DetectCircularReferenceAsync(tuple.Id, parentId.Value);
                    return !isCircular;
                })
                .WithMessage("Lỗi tham chiếu vòng: Không thể chọn cấp dưới làm cha của cấp trên.");
        }

        private async Task<bool> DetectCircularReferenceAsync(int categoryId, int newParentId)
        {
            var allCategories = await _categoryRepo.GetAllCategoryParentMapAsync();

            var currentId = newParentId;
            var visited = new HashSet<int>();

            while (allCategories.ContainsKey(currentId))
            {
                if (currentId == categoryId) return true;
                if (!visited.Add(currentId)) return true;

                var parentId = allCategories[currentId];
                if (!parentId.HasValue) return false;
                currentId = parentId.Value;
            }

            return false;
        }
    }
}
