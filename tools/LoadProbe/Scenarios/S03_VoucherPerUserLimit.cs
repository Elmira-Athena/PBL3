using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Shared.DTOs.Sale;
using PBL3.Tools.LoadProbe.Infra;

namespace PBL3.Tools.LoadProbe.Scenarios;

/// <summary>
/// Một khách, voucher MaxUsesPerUser = 1, bắn 10 request cùng lúc.
///
/// Khác kịch bản S02 ở chỗ hạn mức nằm trên CẶP (voucher, người dùng) chứ không
/// trên tổng lượt — nên nó không được bảo vệ bởi cùng một câu UPDATE có điều kiện,
/// và đây chính là chỗ đợt 3 định thêm unique index (UserId, VoucherId).
/// </summary>
public sealed class S03_VoucherPerUserLimit : ScenarioBase
{
    private const int Attempts = 10;
    private const string VoucherCode = "LP-VMU";

    /// <summary>
    /// Mã như NGƯỜI DÙNG gõ — cố ý khác hoa/thường với <see cref="VoucherCode"/> đã lưu.
    /// </summary>
    /// <remarks>
    /// 🚨 Trước đợt 7, kịch bản này seed và gửi CÙNG MỘT hằng, nên nó <b>mù</b> với việc tra mã
    /// có phân biệt hoa/thường hay không. SQL Server dùng collation CI mặc định nên bất biến
    /// "gõ thường vẫn khớp mã hoa" đang đúng — nhưng đúng <b>nhờ cấu hình DB</b>, không nhờ dòng
    /// code nào: không có <c>HasCollation</c> nào trong repo, và ba đường GHI của các cột này
    /// (<c>ProductVariantService</c>, <c>ProductService</c>, <c>ImportReceiptService</c>) thậm
    /// chí không chuẩn hoá.
    ///
    /// Tách hai hằng biến kịch bản sẵn có thành phép đo trực tiếp cho quyết định lớn nhất của
    /// đợt chuyển PostgreSQL: <c>Vouchers.Code</c> có phải <c>citext</c> không. Trên PostgreSQL
    /// KHÔNG có citext, <c>GetByCodesWithCategoriesAsync</c> trả rỗng ⇒ kịch bản này đỏ.
    ///
    /// ⚠️ Phải chạy XANH trên SQL Server trước khi chuyển — đó là ca đối chứng. Bỏ qua bước đó
    /// thì một lần đỏ về sau không phân biệt được "citext hỏng" với "kịch bản vốn đã sai".
    /// </remarks>
    private const string VoucherCodeAsTyped = "lp-vmu";


    private ProbeCustomer _customer = null!;
    private int _voucherId;

    public override string Id => "S03";
    public override string Title => "Cùng một khách, MaxUsesPerUser = 1, 10 request";
    public override string Invariant =>
        "COUNT(VoucherUsages WHERE UserId = khách AND VoucherId = voucher) = 1";
    public override int ExpectedRequests => Attempts;

    protected override async Task SetupCoreAsync(ProbeEnvironment env)
    {
        await Fixture.EnsureCatalogAsync(Attempts + 10);
        _customer = (await Fixture.CreateCustomersAsync(1)).Single();

        await using var db = env.NewDbContext();

        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM \"VoucherUsages\" WHERE \"VoucherId\" IN (SELECT \"Id\" FROM \"Vouchers\" WHERE \"Code\" = {0});" +
            "DELETE FROM \"Vouchers\" WHERE \"Code\" = {0};", VoucherCode);

        var voucher = new Voucher
        {
            Code = VoucherCode,
            Name = "LoadProbe — mỗi người 1 lượt",
            DiscountType = 0,
            DiscountValue = 10_000m,
            MinOrderValue = 0,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1),
            Quantity = null,           // không giới hạn tổng lượt — chỉ giới hạn theo người
            UsedCount = 0,
            MaxUsesPerUser = 1,
            ApplyFor = 0,
            IsStackable = false,
            IsActive = true,
            CreatedDate = DateTime.UtcNow
        };
        db.Vouchers.Add(voucher);
        await db.SaveChangesAsync();
        _voucherId = voucher.Id;
    }

    public override Task<FireReport> FireAsync(ProbeEnvironment env)
        => ConcurrentFire.FireAsync(Attempts, _ =>
            env.PostAsync("api/orders/checkout", new CheckoutRequest
            {
                UserAddressId = _customer.AddressId,
                IsBuyNow = true,
                BuyNowVariantId = Fixture.VariantId,
                BuyNowQuantity = 1,
                PaymentMethod = 0,
                ShippingFee = 0,
                VoucherCodes = new List<string> { VoucherCodeAsTyped },
                Note = "LoadProbe S03"
            }, _customer.Token));

    public override async Task<ProbeOutcome> AssertAsync(ProbeEnvironment env, FireReport fire)
    {
        await using var db = env.NewDbContext();

        var usages = await db.VoucherUsages
            .AsNoTracking()
            .CountAsync(u => u.VoucherId == _voucherId && u.UserId == _customer.UserId);

        var details = new[]
        {
            $"COUNT(VoucherUsages) cho cặp (khách, voucher) = {usages} (kỳ vọng 1)",
            $"Mã HTTP: {fire.Histogram()}"
        };

        if (usages > 1)
        {
            return ProbeOutcome.Fail(
                $"Khách dùng được {usages} lượt trong khi MaxUsesPerUser = 1.", details);
        }

        if (usages == 0)
        {
            return ProbeOutcome.Inconclusive(
                "Không lượt nào được ghi nhận. Hai giả thuyết, kiểm theo đúng thứ tự này: " +
                "(1) tra mã PHÂN BIỆT HOA/THƯỜNG — kịch bản seed 'LP-VMU' nhưng gửi 'lp-vmu', " +
                "nên trên PostgreSQL thiếu citext ở Vouchers.Code thì mọi request nhận " +
                "\"Mã giảm giá không tồn tại\"; (2) lỗi seed. Xem mã HTTP: toàn 400 nghiêng về (1), " +
                "toàn 500 nghiêng về (2).", details);
        }

        return ProbeOutcome.Pass("Đúng 1 lượt cho mỗi người.", details);
    }
}
