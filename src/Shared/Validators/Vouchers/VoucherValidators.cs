using FluentValidation;
using PBL3.Shared.DTOs.Vouchers;

namespace PBL3.Shared.Validators.Vouchers
{
    public class CreateVoucherRequestValidator : AbstractValidator<CreateVoucherRequest>
    {
        public CreateVoucherRequestValidator()
        {
            RuleFor(x => x.Code)
                .NotEmpty().WithMessage("Mã voucher không được để trống.")
                .MaximumLength(50).WithMessage("Mã voucher không được vượt quá 50 ký tự.")
                .Matches("^[A-Z0-9_-]+$").WithMessage("Mã voucher chỉ được dùng chữ hoa, số và ký tự - _.");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Tên voucher không được để trống.")
                .MaximumLength(200).WithMessage("Tên voucher không được vượt quá 200 ký tự.");

            RuleFor(x => x.DiscountType)
                .InclusiveBetween((byte)0, (byte)1).WithMessage("Loại giảm giá không hợp lệ. Chỉ chấp nhận 0 (tiền cố định) hoặc 1 (phần trăm).");

            RuleFor(x => x.DiscountValue)
                .GreaterThan(0).WithMessage("Giá trị giảm phải lớn hơn 0.");

            RuleFor(x => x.DiscountValue)
                .LessThanOrEqualTo(100)
                .WithMessage("Phần trăm giảm không được vượt quá 100%.")
                .When(x => x.DiscountType == 1);

            RuleFor(x => x.MinOrderValue)
                .GreaterThanOrEqualTo(0).WithMessage("Giá trị đơn hàng tối thiểu không được âm.");

            RuleFor(x => x.MaxDiscountAmount)
                .GreaterThan(0).WithMessage("Số tiền giảm tối đa phải lớn hơn 0.")
                .When(x => x.MaxDiscountAmount.HasValue);

            RuleFor(x => x.StartDate)
                .NotEmpty().WithMessage("Ngày bắt đầu không được để trống.");

            RuleFor(x => x.EndDate)
                .NotEmpty().WithMessage("Ngày kết thúc không được để trống.")
                .GreaterThan(x => x.StartDate).WithMessage("Ngày kết thúc phải sau ngày bắt đầu.");

            RuleFor(x => x.Quantity)
                .GreaterThan(0).WithMessage("Số lượng phát hành phải lớn hơn 0.")
                .When(x => x.Quantity.HasValue);

            RuleFor(x => x.MaxUsesPerUser)
                .GreaterThan(0).WithMessage("Số lần sử dụng tối đa mỗi khách phải lớn hơn 0.")
                .When(x => x.MaxUsesPerUser.HasValue);

            RuleFor(x => x.ApplyFor)
                .InclusiveBetween((byte)0, (byte)2).WithMessage("Kênh áp dụng không hợp lệ. Chỉ chấp nhận 0 (Cả hai), 1 (Online), 2 (Quầy).");

            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("Mô tả không được vượt quá 500 ký tự.")
                .When(x => !string.IsNullOrWhiteSpace(x.Description));
        }
    }

    public class UpdateVoucherRequestValidator : AbstractValidator<UpdateVoucherRequest>
    {
        public UpdateVoucherRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Tên voucher không được để trống.")
                .MaximumLength(200).WithMessage("Tên voucher không được vượt quá 200 ký tự.");

            RuleFor(x => x.DiscountType)
                .InclusiveBetween((byte)0, (byte)1).WithMessage("Loại giảm giá không hợp lệ. Chỉ chấp nhận 0 (tiền cố định) hoặc 1 (phần trăm).");

            RuleFor(x => x.DiscountValue)
                .GreaterThan(0).WithMessage("Giá trị giảm phải lớn hơn 0.");

            RuleFor(x => x.DiscountValue)
                .LessThanOrEqualTo(100)
                .WithMessage("Phần trăm giảm không được vượt quá 100%.")
                .When(x => x.DiscountType == 1);

            RuleFor(x => x.MinOrderValue)
                .GreaterThanOrEqualTo(0).WithMessage("Giá trị đơn hàng tối thiểu không được âm.");

            RuleFor(x => x.MaxDiscountAmount)
                .GreaterThan(0).WithMessage("Số tiền giảm tối đa phải lớn hơn 0.")
                .When(x => x.MaxDiscountAmount.HasValue);

            RuleFor(x => x.StartDate)
                .NotEmpty().WithMessage("Ngày bắt đầu không được để trống.");

            RuleFor(x => x.EndDate)
                .NotEmpty().WithMessage("Ngày kết thúc không được để trống.")
                .GreaterThan(x => x.StartDate).WithMessage("Ngày kết thúc phải sau ngày bắt đầu.");

            RuleFor(x => x.Quantity)
                .GreaterThan(0).WithMessage("Số lượng phát hành phải lớn hơn 0.")
                .When(x => x.Quantity.HasValue);

            RuleFor(x => x.MaxUsesPerUser)
                .GreaterThan(0).WithMessage("Số lần sử dụng tối đa mỗi khách phải lớn hơn 0.")
                .When(x => x.MaxUsesPerUser.HasValue);

            RuleFor(x => x.ApplyFor)
                .InclusiveBetween((byte)0, (byte)2).WithMessage("Kênh áp dụng không hợp lệ. Chỉ chấp nhận 0 (Cả hai), 1 (Online), 2 (Quầy).");

            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("Mô tả không được vượt quá 500 ký tự.")
                .When(x => !string.IsNullOrWhiteSpace(x.Description));
        }
    }
}
