using FluentValidation;
using PBL3.Shared.DTOs.Products;

namespace PBL3.Shared.Validators
{
    // ========================================================
    // CREATE PRODUCT
    // ========================================================
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

    // ========================================================
    // CREATE VARIANT (nested inside CreateProduct)
    // ========================================================
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

    // ========================================================
    // UPDATE PRODUCT
    // ========================================================
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

    // ========================================================
    // SAVE VARIANT (Add/Update Variant in existing Product)
    // ========================================================
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
