using FluentValidation;
using PBL3.Shared.DTOs.Inventory;
using System.Linq;

namespace PBL3.Shared.Validators.Inventory
{
    public class ExportOrderRequestValidator : AbstractValidator<ExportOrderRequest>
    {
        public ExportOrderRequestValidator()
        {
            RuleFor(x => x.OrderId)
                .GreaterThan(0).WithMessage("Mã đơn hàng không hợp lệ.");

            RuleFor(x => x.Details)
                .NotEmpty().WithMessage("Danh sách sản phẩm xuất kho không được để trống.");

            RuleForEach(x => x.Details)
                .SetValidator(new ExportOrderDetailRequestValidator());

            // Check if there are any duplicate serial numbers across the entire request
            RuleFor(x => x.Details)
                .Must(details =>
                {
                    if (details == null || !details.Any()) return true;
                    var allSerials = details.SelectMany(d => d.SerialNumbers ?? new System.Collections.Generic.List<string>()).ToList();
                    return allSerials.Count == allSerials.Distinct().Count();
                })
                .WithMessage("Có mã Serial bị trùng lặp trong yêu cầu xuất kho.")
                .When(x => x.Details != null && x.Details.Any());
        }
    }

    public class ExportOrderDetailRequestValidator : AbstractValidator<ExportOrderDetailRequest>
    {
        public ExportOrderDetailRequestValidator()
        {
            RuleFor(x => x.OrderDetailId)
                .GreaterThan(0).WithMessage("Mã chi tiết đơn hàng không hợp lệ.");

            RuleFor(x => x.SerialNumbers)
                .NotEmpty().WithMessage("Danh sách Serial không được để trống.");

            RuleForEach(x => x.SerialNumbers)
                .NotEmpty().WithMessage("Mã Serial không được để trống.");
        }
    }
}
