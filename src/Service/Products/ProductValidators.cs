using FluentValidation;
using PBL3.Core.Interfaces;
using PBL3.Shared.DTOs.Products;

namespace PBL3.Service.Products
{
    public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
    {
        public CreateProductRequestValidator(IProductRepository productRepo)
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Tên sản phẩm không được để trống.")
                .MaximumLength(200).WithMessage("Tên sản phẩm không quá 200 ký tự.");

            RuleFor(x => x.ManufacturerId)
                .MustAsync(async (id, cancel) => await productRepo.ManufacturerExistsAsync(id))
                .WithMessage("Nhà sản xuất không tồn tại.");

            RuleFor(x => x.CategoryId)
                .MustAsync(async (id, cancel) => await productRepo.CategoryExistsAsync(id))
                .WithMessage("Danh mục không tồn tại.");

            RuleFor(x => x.Variants)
                .NotEmpty().WithMessage("Sản phẩm phải có ít nhất một phiên bản.");

            RuleForEach(x => x.Variants).ChildRules(v =>
            {
                v.RuleFor(x => x.SKU)
                    .NotEmpty().WithMessage("Mã SKU không được để trống.")
                    .MustAsync(async (sku, cancel) => !await productRepo.IsSkuExistsAsync(sku))
                    .WithMessage(x => $"Mã SKU '{x.SKU}' đã tồn tại trong hệ thống.");
                
                v.RuleFor(x => x.VariantName)
                    .NotEmpty().WithMessage("Tên phiên bản không được để trống.");
                
                v.RuleFor(x => x.Price)
                    .GreaterThan(0).WithMessage("Giá bán phải lớn hơn 0.");
            });

            RuleFor(x => x.Variants)
                .Must(v => v.Select(x => x.SKU.ToUpper()).Distinct().Count() == v.Count)
                .WithMessage("Các phiên bản trong cùng sản phẩm không được trùng mã SKU.");
        }
    }

    public class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
    {
        public UpdateProductRequestValidator(IProductRepository productRepo)
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Tên sản phẩm không được để trống.")
                .MaximumLength(200).WithMessage("Tên sản phẩm không quá 200 ký tự.");

            RuleFor(x => x.ManufacturerId)
                .MustAsync(async (id, cancel) => await productRepo.ManufacturerExistsAsync(id))
                .WithMessage("Nhà sản xuất không tồn tại.");

            RuleFor(x => x.CategoryId)
                .MustAsync(async (id, cancel) => await productRepo.CategoryExistsAsync(id))
                .WithMessage("Danh mục không tồn tại.");
        }
    }

    public class SaveVariantRequestValidator : AbstractValidator<SaveVariantRequest>
    {
        public SaveVariantRequestValidator(IProductRepository productRepo)
        {
            RuleFor(x => x.SKU)
                .NotEmpty().WithMessage("Mã SKU không được để trống.")
                .MustAsync(async (req, sku, cancel) => !await productRepo.IsSkuExistsAsync(sku, excludeVariantId: req.Id))
                .WithMessage(x => $"Mã SKU '{x.SKU}' đã tồn tại trong hệ thống.");

            RuleFor(x => x.VariantName)
                .NotEmpty().WithMessage("Tên phiên bản không được để trống.");

            RuleFor(x => x.Price)
                .GreaterThan(0).WithMessage("Giá bán phải lớn hơn 0.");
        }
    }
}
