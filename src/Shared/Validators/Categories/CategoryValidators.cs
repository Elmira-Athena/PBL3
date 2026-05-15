using FluentValidation;
using PBL3.Shared.DTOs.Categories;

namespace PBL3.Shared.Validators.Categories
{
    public class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
    {
        public CreateCategoryRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Tên danh mục không được để trống.")
                .MaximumLength(100).WithMessage("Tên danh mục không được vượt quá 100 ký tự.");

            RuleFor(x => x.Slug)
                .NotEmpty().WithMessage("Slug không được để trống.")
                .MaximumLength(150).WithMessage("Slug không được vượt quá 150 ký tự.")
                .Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$").WithMessage("Slug chỉ chấp nhận chữ thường, số và dấu gạch ngang.");

            RuleFor(x => x.SortOrder)
                .GreaterThanOrEqualTo(0).WithMessage("Thứ tự hiển thị phải >= 0.");
        }
    }

    public class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
    {
        public UpdateCategoryRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Tên danh mục không được để trống.")
                .MaximumLength(100).WithMessage("Tên danh mục không được vượt quá 100 ký tự.");

            RuleFor(x => x.Slug)
                .NotEmpty().WithMessage("Slug không được để trống.")
                .MaximumLength(150).WithMessage("Slug không được vượt quá 150 ký tự.")
                .Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$").WithMessage("Slug chỉ chấp nhận chữ thường, số và dấu gạch ngang.");

            RuleFor(x => x.SortOrder)
                .GreaterThanOrEqualTo(0).WithMessage("Thứ tự hiển thị phải >= 0.");
        }
    }
}
