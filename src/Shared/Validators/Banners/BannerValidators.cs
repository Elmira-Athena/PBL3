using FluentValidation;
using PBL3.Shared.DTOs.Banners;

namespace PBL3.Shared.Validators.Banners
{
    /// <summary>
    /// Validator cho banner. <see cref="UpdateBannerRequest"/> kế thừa <see cref="CreateBannerRequest"/>
    /// nên dùng chung validator này thông qua <see cref="UpdateBannerRequestValidator"/>.
    /// </summary>
    public class CreateBannerRequestValidator : AbstractValidator<CreateBannerRequest>
    {
        public CreateBannerRequestValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Tiêu đề banner không được để trống.")
                .MaximumLength(200).WithMessage("Tiêu đề banner không được vượt quá 200 ký tự.");

            RuleFor(x => x.ImageUrl)
                .NotEmpty().WithMessage("Ảnh banner không được để trống. Vui lòng tải lên ảnh trước khi lưu.")
                .MaximumLength(500).WithMessage("Đường dẫn ảnh không được vượt quá 500 ký tự.");

            RuleFor(x => x.LinkUrl)
                .MaximumLength(500).WithMessage("Đường dẫn liên kết không được vượt quá 500 ký tự.")
                .Must(BeAValidLink).WithMessage("Đường dẫn liên kết phải bắt đầu bằng http://, https:// hoặc / (đường dẫn nội bộ).")
                .When(x => !string.IsNullOrWhiteSpace(x.LinkUrl));

            RuleFor(x => x.SortOrder)
                .GreaterThanOrEqualTo(0).WithMessage("Thứ tự hiển thị phải là số nguyên không âm.");

            RuleFor(x => x)
                .Must(x => !x.StartDate.HasValue || !x.EndDate.HasValue || x.EndDate.Value >= x.StartDate.Value)
                .WithMessage("Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.");
        }

        private static bool BeAValidLink(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return true;
            return url.StartsWith("http://") || url.StartsWith("https://") || url.StartsWith("/");
        }
    }

    /// <summary>
    /// Update dùng chung rule với Create (DTO kế thừa).
    /// </summary>
    public class UpdateBannerRequestValidator : AbstractValidator<UpdateBannerRequest>
    {
        public UpdateBannerRequestValidator()
        {
            Include(new CreateBannerRequestValidator());
        }
    }
}
