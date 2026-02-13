using FluentValidation;
using PBL3.Shared.DTOs.Inventory;

namespace PBL3.Shared.Validators
{
    /// <summary>
    /// Validator cho từng dòng chi tiết phiếu nhập.
    /// </summary>
    public class ImportReceiptDetailRequestValidator : AbstractValidator<ImportReceiptDetailRequest>
    {
        public ImportReceiptDetailRequestValidator()
        {
            RuleFor(x => x.VariantId)
                .GreaterThan(0).WithMessage("Mã biến thể sản phẩm không hợp lệ.");

            RuleFor(x => x.Quantity)
                .GreaterThan(0).WithMessage("Số lượng nhập phải lớn hơn 0.");

            RuleFor(x => x.ImportPrice)
                .GreaterThanOrEqualTo(0).WithMessage("Giá nhập phải lớn hơn hoặc bằng 0.");

            RuleFor(x => x.SerialNumbers)
                .NotEmpty().WithMessage("Danh sách Serial không được để trống.");

            // **CRITICAL**: Số lượng Serial phải khớp tuyệt đối với Quantity
            RuleFor(x => x)
                .Must(x => x.SerialNumbers != null && x.SerialNumbers.Count == x.Quantity)
                .WithMessage("Số lượng Serial phải khớp chính xác với số lượng nhập (Quantity).")
                .When(x => x.SerialNumbers != null && x.SerialNumbers.Count > 0);

            // Mỗi Serial không được trống
            RuleForEach(x => x.SerialNumbers)
                .NotEmpty().WithMessage("Mã Serial không được để trống.")
                .MaximumLength(100).WithMessage("Mã Serial không được vượt quá 100 ký tự.");
        }
    }

    /// <summary>
    /// Validator cho request tạo phiếu nhập kho.
    /// </summary>
    public class CreateImportReceiptRequestValidator : AbstractValidator<CreateImportReceiptRequest>
    {
        public CreateImportReceiptRequestValidator()
        {
            RuleFor(x => x.SupplierId)
                .GreaterThan(0).WithMessage("Nhà cung cấp không hợp lệ.");

            RuleFor(x => x.Note)
                .MaximumLength(500).WithMessage("Ghi chú không được vượt quá 500 ký tự.")
                .When(x => !string.IsNullOrWhiteSpace(x.Note));

            RuleFor(x => x.Details)
                .NotEmpty().WithMessage("Phiếu nhập phải có ít nhất 1 dòng chi tiết.");

            RuleForEach(x => x.Details)
                .SetValidator(new ImportReceiptDetailRequestValidator());

            // Check trùng Serial nội bộ trong toàn bộ request
            RuleFor(x => x.Details)
                .Must(details =>
                {
                    if (details == null || details.Count == 0) return true;
                    var allSerials = details.SelectMany(d => d.SerialNumbers ?? new List<string>()).ToList();
                    return allSerials.Count == allSerials.Distinct(StringComparer.OrdinalIgnoreCase).Count();
                })
                .WithMessage("Có mã Serial bị trùng lặp trong phiếu nhập. Vui lòng kiểm tra lại.");
        }
    }
}
