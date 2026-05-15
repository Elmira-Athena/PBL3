using FluentValidation;
using PBL3.Shared.DTOs.Customers;
using System;

namespace PBL3.Shared.Validators.Customers
{
    public class CreateCustomerRequestValidator : AbstractValidator<CreateCustomerRequest>
    {
        public CreateCustomerRequestValidator()
        {
            RuleFor(x => x.FullName)
                .NotEmpty().WithMessage("Họ tên không được để trống.")
                .MaximumLength(100).WithMessage("Họ tên không được vượt quá 100 ký tự.");

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email không được để trống.")
                .EmailAddress().WithMessage("Email không đúng định dạng.")
                .MaximumLength(256).WithMessage("Email không được vượt quá 256 ký tự.");

            RuleFor(x => x.PhoneNumber)
                .NotEmpty().WithMessage("Số điện thoại không được để trống.")
                .Matches(@"^0[3-9][0-9]{8}$").WithMessage("Số điện thoại không hợp lệ. Vui lòng nhập số điện thoại Việt Nam gồm 10 chữ số (bắt đầu bằng 03x, 05x, 07x, 08x, 09x).");

            RuleFor(x => x.Address)
                .MaximumLength(255).WithMessage("Địa chỉ không được vượt quá 255 ký tự.");

            RuleFor(x => x.City)
                .MaximumLength(100).WithMessage("Thành phố không được vượt quá 100 ký tự.");

            RuleFor(x => x.DateOfBirth)
                .LessThan(DateTime.Today).WithMessage("Ngày sinh phải nhỏ hơn ngày hiện tại.")
                .When(x => x.DateOfBirth.HasValue);
        }
    }

    public class UpdateCustomerRequestValidator : AbstractValidator<UpdateCustomerRequest>
    {
        public UpdateCustomerRequestValidator()
        {
            RuleFor(x => x.FullName)
                .NotEmpty().WithMessage("Họ tên không được để trống.")
                .MaximumLength(100).WithMessage("Họ tên không được vượt quá 100 ký tự.");

            RuleFor(x => x.Address)
                .MaximumLength(255).WithMessage("Địa chỉ không được vượt quá 255 ký tự.");

            RuleFor(x => x.City)
                .MaximumLength(100).WithMessage("Thành phố không được vượt quá 100 ký tự.");

            RuleFor(x => x.AvatarUrl)
                .MaximumLength(500).WithMessage("Đường dẫn ảnh đại diện không quá 500 ký tự.");

            RuleFor(x => x.DateOfBirth)
                .LessThan(DateTime.Today).WithMessage("Ngày sinh phải nhỏ hơn ngày hiện tại.")
                .When(x => x.DateOfBirth.HasValue);
        }
    }

    public class RegisterCustomerRequestValidator : AbstractValidator<RegisterCustomerRequest>
    {
        public RegisterCustomerRequestValidator()
        {
            RuleFor(x => x.FullName)
                .NotEmpty().WithMessage("Họ tên không được để trống.")
                .MaximumLength(100).WithMessage("Họ tên không được vượt quá 100 ký tự.");

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email không được để trống.")
                .EmailAddress().WithMessage("Email không đúng định dạng.")
                .MaximumLength(256).WithMessage("Email không được vượt quá 256 ký tự.");

            RuleFor(x => x.PhoneNumber)
                .NotEmpty().WithMessage("Số điện thoại không được để trống.")
                .Matches(@"^0[3-9][0-9]{8}$").WithMessage("Số điện thoại không hợp lệ. Vui lòng nhập số điện thoại Việt Nam gồm 10 chữ số (bắt đầu bằng 03x, 05x, 07x, 08x, 09x).");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Mật khẩu không được để trống.")
                .MinimumLength(8).WithMessage("Mật khẩu phải có ít nhất 8 ký tự.")
                .Matches(@"[A-Z]").WithMessage("Mật khẩu phải chứa ít nhất một chữ hoa.")
                .Matches(@"[a-z]").WithMessage("Mật khẩu phải chứa ít nhất một chữ thường.")
                .Matches(@"[0-9]").WithMessage("Mật khẩu phải chứa ít nhất một chữ số.");

            RuleFor(x => x.ConfirmPassword)
                .Equal(x => x.Password).WithMessage("Mật khẩu xác nhận không khớp.");
        }
    }
}
