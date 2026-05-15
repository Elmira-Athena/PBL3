using FluentValidation;
using PBL3.Shared.DTOs.Reviews;

namespace PBL3.Shared.Validators.Reviews
{
    public class CreateReviewRequestValidator : AbstractValidator<CreateReviewRequest>
    {
        public CreateReviewRequestValidator()
        {
            RuleFor(x => x.ProductId)
                .GreaterThan(0).WithMessage("Id sản phẩm không hợp lệ.");

            RuleFor(x => x.Rating)
                .InclusiveBetween((byte)1, (byte)5)
                .WithMessage("Đánh giá phải từ 1 đến 5 sao.");

            RuleFor(x => x.Title)
                .MaximumLength(200)
                .WithMessage("Tiêu đề không được vượt quá 200 ký tự.")
                .When(x => !string.IsNullOrEmpty(x.Title));

            RuleFor(x => x.Content)
                .MaximumLength(2000)
                .WithMessage("Nội dung không được vượt quá 2000 ký tự.")
                .When(x => !string.IsNullOrEmpty(x.Content));
        }
    }
}
