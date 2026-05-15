using FluentValidation;
using PBL3.Shared.DTOs.Products;

namespace PBL3.Shared.Validators.Products
{
    public class CreateVariantRequestValidator : AbstractValidator<CreateVariantRequest>
    {
        public CreateVariantRequestValidator()
        {
            RuleFor(x => x.SKU)
                .NotEmpty().WithMessage("Mã SKU không được để trống.")
                .MaximumLength(50).WithMessage("Mã SKU không được vượt quá 50 ký tự.")
                .Matches(@"^\S+$").WithMessage("Mã SKU không được chứa khoảng trắng.");

            RuleFor(x => x.VariantName)
                .NotEmpty().WithMessage("Tên phiên bản không được để trống.")
                .MaximumLength(200).WithMessage("Tên phiên bản không được vượt quá 200 ký tự.");

            RuleFor(x => x.Price)
                .GreaterThan(0).WithMessage("Giá bán phải lớn hơn 0.");

            RuleFor(x => x.OriginalPrice)
                .GreaterThan(0).When(x => x.OriginalPrice.HasValue)
                .WithMessage("Giá gốc phải lớn hơn 0.");

            RuleFor(x => x.WarrantyMonth)
                .GreaterThanOrEqualTo(0).WithMessage("Thời gian bảo hành phải >= 0 tháng.");
        }
    }

    public class SaveVariantRequestValidator : AbstractValidator<SaveVariantRequest>
    {
        public SaveVariantRequestValidator()
        {
            RuleFor(x => x.SKU)
                .NotEmpty().WithMessage("Mã SKU không được để trống.")
                .MaximumLength(50).WithMessage("Mã SKU không được vượt quá 50 ký tự.")
                .Matches(@"^\S+$").WithMessage("Mã SKU không được chứa khoảng trắng.");

            RuleFor(x => x.VariantName)
                .NotEmpty().WithMessage("Tên phiên bản không được để trống.")
                .MaximumLength(200).WithMessage("Tên phiên bản không được vượt quá 200 ký tự.");

            RuleFor(x => x.Price)
                .GreaterThan(0).WithMessage("Giá bán phải lớn hơn 0.");

            RuleFor(x => x.OriginalPrice)
                .GreaterThan(0).When(x => x.OriginalPrice.HasValue)
                .WithMessage("Giá gốc phải lớn hơn 0.");

            RuleFor(x => x.WarrantyMonth)
                .GreaterThanOrEqualTo(0).WithMessage("Thời gian bảo hành phải >= 0 tháng.");
        }
    }
}
