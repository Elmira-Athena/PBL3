using FluentValidation;
using PBL3.Shared.DTOs.Products;

namespace PBL3.Shared.Validators.Products
{
    public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
    {
        public CreateProductRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Tên sản phẩm không được để trống.")
                .MaximumLength(255).WithMessage("Tên sản phẩm không được vượt quá 255 ký tự.");

            RuleFor(x => x.ManufacturerId)
                .GreaterThan(0).WithMessage("Vui lòng chọn nhà sản xuất.");

            RuleFor(x => x.CategoryId)
                .GreaterThan(0).WithMessage("Vui lòng chọn danh mục.");

            RuleFor(x => x.Variants)
                .NotEmpty().WithMessage("Sản phẩm phải có ít nhất một phiên bản.")
                .Must(v => v != null && v.Count > 0).WithMessage("Sản phẩm phải có ít nhất một phiên bản.");

            RuleForEach(x => x.Variants)
                .SetValidator(new CreateVariantRequestValidator());
        }
    }

    public class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
    {
        public UpdateProductRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Tên sản phẩm không được để trống.")
                .MaximumLength(255).WithMessage("Tên sản phẩm không được vượt quá 255 ký tự.");

            RuleFor(x => x.ManufacturerId)
                .GreaterThan(0).WithMessage("Vui lòng chọn nhà sản xuất.");

            RuleFor(x => x.CategoryId)
                .GreaterThan(0).WithMessage("Vui lòng chọn danh mục.");

            RuleFor(x => x.Status)
                .IsInEnum().WithMessage("Trạng thái sản phẩm không hợp lệ.");
        }
    }
}
