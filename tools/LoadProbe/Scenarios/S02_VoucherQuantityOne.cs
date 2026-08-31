using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Shared.DTOs.Sale;
using PBL3.Tools.LoadProbe.Infra;

namespace PBL3.Tools.LoadProbe.Scenarios;

/// <summary>
/// Voucher chỉ còn ĐÚNG 1 lượt, 20 khách khác nhau dùng cùng lúc.
///
/// Đây là lỗi bán vượt kinh điển và nó KHÔNG hiện ra qua mã HTTP: cả 20 request
/// đều trả 200, chỉ có cột UsedCount là 20. Bất biến phải kiểm ở DB.
/// </summary>
public sealed class S02_VoucherQuantityOne : ScenarioBase
{
    private const int Customers = 20;
    private const string VoucherCode = "LP-VQ1";

    private List<ProbeCustomer> _customers = new();
    private int _voucherId;

    public override string Id => "S02";
    public override string Title => "Voucher Quantity = 1, 20 khách dùng đồng thời";
    public override string Invariant =>
        "Voucher.UsedCount = 1 và COUNT(VoucherUsages) = 1";
    public override int ExpectedRequests => Customers;

    protected override async Task SetupCoreAsync(ProbeEnvironment env)
    {
        await Fixture.EnsureCatalogAsync(Customers + 10);
        _customers = await Fixture.CreateCustomersAsync(Customers);

        await using var db = env.NewDbContext();

        // Xoá bản của lần chạy trước để kịch bản chạy lại được nhiều lần.
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM VoucherUsages WHERE VoucherId IN (SELECT Id FROM Vouchers WHERE Code = {0});" +
            "DELETE FROM Vouchers WHERE Code = {0};", VoucherCode);

        var voucher = new Voucher
        {
            Code = VoucherCode,
            Name = "LoadProbe — còn đúng 1 lượt",
            DiscountType = 0,          // 0 = trừ thẳng số tiền
            DiscountValue = 10_000m,
            MinOrderValue = 0,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1),
            Quantity = 1,
            UsedCount = 0,
            MaxUsesPerUser = null,
            ApplyFor = 0,              // 0 = dùng được cả online lẫn POS
            IsStackable = false,
            IsActive = true,
            CreatedDate = DateTime.UtcNow
        };
        db.Vouchers.Add(voucher);
        await db.SaveChangesAsync();
        _voucherId = voucher.Id;
    }

    public override Task<FireReport> FireAsync(ProbeEnvironment env)
        => ConcurrentFire.FireAsync(Customers, i =>
        {
            var customer = _customers[i];
            return env.PostAsync("api/orders/checkout", new CheckoutRequest
            {
                UserAddressId = customer.AddressId,
                IsBuyNow = true,
                BuyNowVariantId = Fixture.VariantId,
                BuyNowQuantity = 1,
                PaymentMethod = 0,
                ShippingFee = 0,
                VoucherCodes = new List<string> { VoucherCode },
                Note = "LoadProbe S02"
            }, customer.Token);
        });

    public override async Task<ProbeOutcome> AssertAsync(ProbeEnvironment env, FireReport fire)
    {
        await using var db = env.NewDbContext();

        var usedCount = await db.Vouchers
            .AsNoTracking()
            .Where(v => v.Id == _voucherId)
            .Select(v => v.UsedCount)
            .FirstAsync();

        var usages = await db.VoucherUsages
            .AsNoTracking()
            .CountAsync(u => u.VoucherId == _voucherId);

        var details = new[]
        {
            $"Voucher.UsedCount = {usedCount} (kỳ vọng 1)",
            $"COUNT(VoucherUsages) = {usages} (kỳ vọng 1)",
            $"Mã HTTP: {fire.Histogram()}"
        };

        if (usedCount != 1 || usages != 1)
        {
            return ProbeOutcome.Fail(
                $"Voucher chỉ còn 1 lượt nhưng ghi nhận UsedCount = {usedCount}, " +
                $"{usages} lượt sử dụng.", details);
        }

        if (fire.Successes == 0)
        {
            return ProbeOutcome.Inconclusive(
                "Không request nào thành công — có thể lỗi seed, không phải kết quả về đồng thời.",
                details);
        }

        return ProbeOutcome.Pass("Đúng 1 lượt được tiêu thụ.", details);
    }
}
