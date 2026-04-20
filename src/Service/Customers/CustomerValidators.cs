using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Shared.DTOs.Customers;

namespace PBL3.Service.Customers
{
    public class CreateCustomerRequestValidator : AbstractValidator<CreateCustomerRequest>
    {
        public CreateCustomerRequestValidator(UserManager<AppUser> userManager)
        {
            RuleFor(x => x.FullName)
                .NotEmpty().WithMessage("Họ tên không được để trống.")
                .MaximumLength(100).WithMessage("Họ tên không quá 100 ký tự.");

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email không được để trống.")
                .EmailAddress().WithMessage("Email không hợp lệ.")
                .MustAsync(async (email, cancel) => 
                {
                    var user = await userManager.FindByEmailAsync(email);
                    return user == null;
                }).WithMessage("Email đã được sử dụng.");

            RuleFor(x => x.PhoneNumber)
                .NotEmpty().WithMessage("Số điện thoại không được để trống.")
                .Matches(@"^\d{10,11}$").WithMessage("Số điện thoại không hợp lệ.");
        }
    }

    public class UpdateCustomerRequestValidator : AbstractValidator<UpdateCustomerRequest>
    {
        public UpdateCustomerRequestValidator()
        {
            RuleFor(x => x.FullName)
                .NotEmpty().WithMessage("Họ tên không được để trống.")
                .MaximumLength(100).WithMessage("Họ tên không quá 100 ký tự.");
        }
    }

    public class RegisterCustomerRequestValidator : AbstractValidator<RegisterCustomerRequest>
    {
        public RegisterCustomerRequestValidator(UserManager<AppUser> userManager)
        {
            RuleFor(x => x.FullName)
                .NotEmpty().WithMessage("Họ tên không được để trống.");

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email không được để trống.")
                .EmailAddress().WithMessage("Email không hợp lệ.")
                .MustAsync(async (email, cancel) => 
                {
                    var user = await userManager.FindByEmailAsync(email);
                    return user == null;
                }).WithMessage("Email đã được sử dụng.");

            RuleFor(x => x.PhoneNumber)
                .NotEmpty().WithMessage("Số điện thoại không được để trống.")
                .Matches(@"^\d{10,11}$").WithMessage("Số điện thoại không hợp lệ.")
                .MustAsync(async (phone, cancel) => 
                {
                    return !await userManager.Users.AnyAsync(u => u.PhoneNumber == phone);
                }).WithMessage("Số điện thoại này đã được sử dụng.");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Mật khẩu không được để trống.")
                .MinimumLength(8).WithMessage("Mật khẩu phải từ 8 ký tự trở lên.");

            RuleFor(x => x.ConfirmPassword)
                .Equal(x => x.Password).WithMessage("Mật khẩu xác nhận không khớp.");
        }
    }
}
