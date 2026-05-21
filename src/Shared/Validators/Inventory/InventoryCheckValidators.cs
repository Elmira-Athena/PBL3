using FluentValidation;
using PBL3.Shared.DTOs.Inventory;

namespace PBL3.Shared.Validators.Inventory
{
    public class CreateInventoryCheckRequestValidator : AbstractValidator<CreateInventoryCheckRequest>
    {
        public CreateInventoryCheckRequestValidator()
        {
            RuleFor(x => x.ScopeType)
                .InclusiveBetween((byte)0, (byte)1)
                .WithMessage("Phạm vi kiểm kê không hợp lệ. Chỉ chấp nhận 0 (Toàn kho) hoặc 1 (Theo danh mục).");

            RuleFor(x => x.ScopeCategoryId)
                .NotNull()
                .GreaterThan(0)
                .WithMessage("Phải chọn danh mục khi phạm vi kiểm kê là theo danh mục.")
                .When(x => x.ScopeType == 1);

            RuleFor(x => x.Note)
                .MaximumLength(500)
                .WithMessage("Ghi chú không được vượt quá 500 ký tự.")
                .When(x => !string.IsNullOrWhiteSpace(x.Note));
        }
    }

    public class ScanSerialRequestValidator : AbstractValidator<ScanSerialRequest>
    {
        public ScanSerialRequestValidator()
        {
            RuleFor(x => x.SerialNumber)
                .NotEmpty()
                .WithMessage("Mã Serial không được để trống.")
                .MaximumLength(100)
                .WithMessage("Mã Serial không được vượt quá 100 ký tự.")
                .Must(s => !s.Contains('\n') && !s.Contains('\r'))
                .WithMessage("Mã Serial không được chứa ký tự xuống dòng.");

            RuleFor(x => x.VariantIdForUnknown)
                .GreaterThan(0)
                .WithMessage("Id biến thể sản phẩm phải lớn hơn 0.")
                .When(x => x.VariantIdForUnknown.HasValue);
        }
    }

    public class UpdateScanReasonRequestValidator : AbstractValidator<UpdateScanReasonRequest>
    {
        public UpdateScanReasonRequestValidator()
        {
            RuleFor(x => x.Reason)
                .NotEmpty()
                .WithMessage("Lý do chênh lệch không được để trống.")
                .MaximumLength(500)
                .WithMessage("Lý do không được vượt quá 500 ký tự.");

            RuleFor(x => x.ProposedActionNote)
                .MaximumLength(200)
                .WithMessage("Hướng xử lý đề xuất không được vượt quá 200 ký tự.")
                .When(x => !string.IsNullOrWhiteSpace(x.ProposedActionNote));
        }
    }

    public class RejectInventoryCheckRequestValidator : AbstractValidator<RejectInventoryCheckRequest>
    {
        public RejectInventoryCheckRequestValidator()
        {
            RuleFor(x => x.Reason)
                .NotEmpty()
                .WithMessage("Lý do từ chối không được để trống.")
                .MaximumLength(500)
                .WithMessage("Lý do từ chối không được vượt quá 500 ký tự.");
        }
    }
}
