using System.Linq;
using FluentValidation;
using PBL3.Shared.DTOs.Sale;

namespace PBL3.Shared.Validators.Sale
{
    public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
    {
        public CreateOrderRequestValidator()
        {
            RuleFor(x => x.ShipName).NotEmpty().WithMessage("Tên người nhận không được để trống.");
            RuleFor(x => x.ShipPhone).NotEmpty().WithMessage("Số điện thoại không được để trống.")
                .Matches(@"^0\d{9}$").WithMessage("Số điện thoại không hợp lệ.");
            RuleFor(x => x.ShipAddress).NotEmpty().WithMessage("Địa chỉ giao hàng không được để trống.");
            RuleFor(x => x.ShipCity).NotEmpty().WithMessage("Thành phố không được để trống.");

            RuleFor(x => x.Items).NotEmpty().WithMessage("Đơn hàng phải có ít nhất một sản phẩm.");
            RuleForEach(x => x.Items).ChildRules(item =>
            {
                item.RuleFor(i => i.VariantId).GreaterThan(0).WithMessage("Mã sản phẩm không hợp lệ.");
                item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("Số lượng phải lớn hơn 0.");
            });

            // Voucher codes: Nếu có thì không được trùng lặp nội bộ
            RuleFor(x => x.VoucherCodes)
                .Must(codes => codes == null || codes.Distinct().Count() == codes.Count)
                .WithMessage("Danh sách mã giảm giá không được chứa mã trùng lặp.");
        }
    }
}
