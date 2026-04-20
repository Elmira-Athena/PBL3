using FluentValidation;
using PBL3.Shared.DTOs.Suppliers;

namespace PBL3.Service.Suppliers
{
    public class CreateSupplierRequestValidator : AbstractValidator<CreateSupplierRequest>
    {
        public CreateSupplierRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Tên nhà cung cấp không được để trống.")
                .MaximumLength(200).WithMessage("Tên nhà cung cấp không quá 200 ký tự.");

            RuleFor(x => x.PhoneNumber)
                .NotEmpty().WithMessage("Số điện thoại không được để trống.")
                .Matches(@"^\d{10,11}$").WithMessage("Số điện thoại không hợp lệ (10-11 số).");

            RuleFor(x => x.Email)
                .EmailAddress().When(x => !string.IsNullOrEmpty(x.Email))
                .WithMessage("Email không hợp lệ.");
        }
    }

    public class UpdateSupplierRequestValidator : AbstractValidator<(int Id, UpdateSupplierRequest Request)>
    {
        public UpdateSupplierRequestValidator()
        {
            RuleFor(x => x.Request.Name)
                .NotEmpty().WithMessage("Tên nhà cung cấp không được để trống.")
                .MaximumLength(200).WithMessage("Tên nhà cung cấp không quá 200 ký tự.");

            RuleFor(x => x.Request.PhoneNumber)
                .NotEmpty().WithMessage("Số điện thoại không được để trống.")
                .Matches(@"^\d{10,11}$").WithMessage("Số điện thoại không hợp lệ (10-11 số).");

            RuleFor(x => x.Request.Email)
                .EmailAddress().When(x => !string.IsNullOrEmpty(x.Request.Email))
                .WithMessage("Email không hợp lệ.");
        }
    }
}
