using FluentValidation;
using PBL3.Shared.DTOs.Suppliers;

namespace PBL3.Shared.Validators
{
    public class CreateSupplierRequestValidator : AbstractValidator<CreateSupplierRequest>
    {
        public CreateSupplierRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Tên nhà cung cấp không được để trống.")
                .MaximumLength(200).WithMessage("Tên nhà cung cấp không được vượt quá 200 ký tự.");

            RuleFor(x => x.PhoneNumber)
                .NotEmpty().WithMessage("Số điện thoại không được để trống.")
                .MaximumLength(20).WithMessage("Số điện thoại không được vượt quá 20 ký tự.")
                .Matches(@"^[0-9]+$").WithMessage("Số điện thoại chỉ được chứa chữ số.");

            RuleFor(x => x.Email)
                .EmailAddress().WithMessage("Email không đúng định dạng.")
                .When(x => !string.IsNullOrWhiteSpace(x.Email));

            RuleFor(x => x.TaxCode)
                .MaximumLength(20).WithMessage("Mã số thuế không được vượt quá 20 ký tự.")
                .When(x => !string.IsNullOrWhiteSpace(x.TaxCode));

            RuleFor(x => x.ContactPerson)
                .MaximumLength(100).WithMessage("Tên người liên hệ không được vượt quá 100 ký tự.")
                .When(x => !string.IsNullOrWhiteSpace(x.ContactPerson));

            RuleFor(x => x.Address)
                .MaximumLength(255).WithMessage("Địa chỉ không được vượt quá 255 ký tự.")
                .When(x => !string.IsNullOrWhiteSpace(x.Address));
        }
    }

    public class UpdateSupplierRequestValidator : AbstractValidator<UpdateSupplierRequest>
    {
        public UpdateSupplierRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Tên nhà cung cấp không được để trống.")
                .MaximumLength(200).WithMessage("Tên nhà cung cấp không được vượt quá 200 ký tự.");

            RuleFor(x => x.PhoneNumber)
                .NotEmpty().WithMessage("Số điện thoại không được để trống.")
                .MaximumLength(20).WithMessage("Số điện thoại không được vượt quá 20 ký tự.")
                .Matches(@"^[0-9]+$").WithMessage("Số điện thoại chỉ được chứa chữ số.");

            RuleFor(x => x.Email)
                .EmailAddress().WithMessage("Email không đúng định dạng.")
                .When(x => !string.IsNullOrWhiteSpace(x.Email));

            RuleFor(x => x.TaxCode)
                .MaximumLength(20).WithMessage("Mã số thuế không được vượt quá 20 ký tự.")
                .When(x => !string.IsNullOrWhiteSpace(x.TaxCode));

            RuleFor(x => x.ContactPerson)
                .MaximumLength(100).WithMessage("Tên người liên hệ không được vượt quá 100 ký tự.")
                .When(x => !string.IsNullOrWhiteSpace(x.ContactPerson));

            RuleFor(x => x.Address)
                .MaximumLength(255).WithMessage("Địa chỉ không được vượt quá 255 ký tự.")
                .When(x => !string.IsNullOrWhiteSpace(x.Address));
        }
    }
}
