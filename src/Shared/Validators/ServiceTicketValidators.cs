using FluentValidation;
using PBL3.Shared.DTOs.ServiceTickets;

namespace PBL3.Shared.Validators
{
    public class ServiceTicketIntakeRequestValidator : AbstractValidator<ServiceTicketIntakeRequestDto>
    {
        public ServiceTicketIntakeRequestValidator()
        {
            RuleFor(x => x.SerialNumber)
                .NotEmpty().WithMessage("Mã Serial không được để trống.");

            RuleFor(x => x.CustomerReportedIssue)
                .NotEmpty().WithMessage("Vui lòng nhập mô tả vấn đề khách báo cáo.")
                .MaximumLength(2000).WithMessage("Mô tả không được vượt quá 2000 ký tự.");

            RuleFor(x => x.CosmeticNotes)
                .MaximumLength(1000).WithMessage("Ghi chú tình trạng không được vượt quá 1000 ký tự.");

            RuleFor(x => x.WalkInCustomerName)
                .MaximumLength(100).WithMessage("Tên khách hàng không được vượt quá 100 ký tự.");

            RuleFor(x => x.WalkInCustomerPhone)
                .MaximumLength(20).WithMessage("Số điện thoại không được vượt quá 20 ký tự.");
        }
    }

    public class ServiceTicketDiagnosisValidator : AbstractValidator<ServiceTicketDiagnosisDto>
    {
        public ServiceTicketDiagnosisValidator()
        {
            RuleFor(x => x.DiagnosisFindings)
                .NotEmpty().WithMessage("Vui lòng nhập kết luận chẩn đoán.")
                .MaximumLength(2000).WithMessage("Kết luận không được vượt quá 2000 ký tự.");
        }
    }

    public class QuotationCreateValidator : AbstractValidator<QuotationCreateDto>
    {
        public QuotationCreateValidator()
        {
            RuleFor(x => x.LaborCost)
                .GreaterThanOrEqualTo(0).WithMessage("Phí công không được âm.");

            RuleFor(x => x.Items)
                .NotEmpty().WithMessage("Phải có ít nhất một mục trong báo giá.")
                .Must(x => x.All(i => i.Quantity > 0)).WithMessage("Số lượng phải lớn hơn 0.")
                .Must(x => x.All(i => i.UnitPrice >= 0)).WithMessage("Giá không được âm.")
                .Must(x => x.All(i => !string.IsNullOrWhiteSpace(i.Description))).WithMessage("Mô tả mục không được để trống.");
        }
    }

    public class RmaShipmentCreateValidator : AbstractValidator<RmaShipmentCreateDto>
    {
        public RmaShipmentCreateValidator()
        {
            RuleFor(x => x.CarrierName)
                .NotEmpty().WithMessage("Tên hãng vận chuyển không được để trống.")
                .MaximumLength(100).WithMessage("Tên hãng không được vượt quá 100 ký tự.");

            RuleFor(x => x.TrackingCode)
                .NotEmpty().WithMessage("Mã vận đơn không được để trống.")
                .MaximumLength(100).WithMessage("Mã vận đơn không được vượt quá 100 ký tự.");
        }
    }

    public class RmaResolutionUpdateValidator : AbstractValidator<RmaResolutionUpdateDto>
    {
        public RmaResolutionUpdateValidator()
        {
            RuleFor(x => x.ManufacturerResolution)
                .Must(x => x >= 1 && x <= 3).WithMessage("Kết quả xử lý không hợp lệ.");

            RuleFor(x => x.ReplacementSerialId)
                .NotNull().When(x => x.ManufacturerResolution == 2).WithMessage("Phải chọn Serial thay thế khi hãng đổi máy.");

            RuleFor(x => x.ManufacturerNotes)
                .MaximumLength(1000).WithMessage("Ghi chú từ hãng không được vượt quá 1000 ký tự.");
        }
    }

    public class ServiceTicketCancelValidator : AbstractValidator<ServiceTicketCancelDto>
    {
        public ServiceTicketCancelValidator()
        {
            RuleFor(x => x.CancelReason)
                .NotEmpty().WithMessage("Vui lòng nhập lý do hủy phiếu.")
                .MaximumLength(500).WithMessage("Lý do hủy không được vượt quá 500 ký tự.");
        }
    }

    public class QuotationAcceptValidator : AbstractValidator<QuotationAcceptDto>
    {
        public QuotationAcceptValidator()
        {
            RuleFor(x => x.NextStatus)
                .Must(x => x == 4 || x == 5).WithMessage("Trạng thái tiếp theo không hợp lệ.");
        }
    }

    public class QuotationRejectValidator : AbstractValidator<QuotationRejectDto>
    {
        public QuotationRejectValidator()
        {
            RuleFor(x => x.Reason)
                .NotEmpty().WithMessage("Vui lòng nhập lý do từ chối báo giá.")
                .MaximumLength(1000).WithMessage("Lý do từ chối không được vượt quá 1000 ký tự.");
        }
    }
}
