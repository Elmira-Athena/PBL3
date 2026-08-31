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
            "DELETE FROM VoucherUsages WHERE VoucherId IN (SELECT Id FROM Vouchers WHERE Code = {0});" +
            "DELETE FROM Vouchers WHERE Code = {0};", VoucherCode);

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
                VoucherCodes = new List<string> { VoucherCode },
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
                "Không lượt nào được ghi nhận — kiểm lại seed trước khi kết luận.", details);
        }

        return ProbeOutcome.Pass("Đúng 1 lượt cho mỗi người.", details);
    }
}
