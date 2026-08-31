using Microsoft.EntityFrameworkCore;
using PBL3.Shared.DTOs.Sale;
using PBL3.Tools.LoadProbe.Infra;

namespace PBL3.Tools.LoadProbe.Scenarios;

/// <summary>
/// 50 khách hàng checkout cùng lúc trên cùng một biến thể còn dư hàng.
///
/// Đây là kịch bản kiểm mã chứng từ: 50 đơn phải ra 50 mã KHÁC NHAU. Trước khi có
/// IDocumentCodeGenerator, mã đơn sinh bằng "đọc mã cuối trong ngày rồi +1" — hai
/// request đọc cùng một mã cuối là ra hai đơn trùng mã.
/// </summary>
public sealed class S01_ConcurrentCheckout : ScenarioBase
{
    private const int Customers = 50;

    private List<ProbeCustomer> _customers = new();

    public override string Id => "S01";
    public override string Title => "50 khách checkout đồng thời";
    public override string Invariant =>
        "Không có OrderCode trùng, đủ 50 đơn, 0 lỗi 5xx";
    public override int ExpectedRequests => Customers;

    protected override async Task SetupCoreAsync(ProbeEnvironment env)
    {
        // Dư hàng có chủ đích: kịch bản này đo mã chứng từ, không đo tranh chấp tồn kho.
        await Fixture.EnsureCatalogAsync(Customers + 10);
        _customers = await Fixture.CreateCustomersAsync(Customers);
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
                Note = "LoadProbe S01"
            }, customer.Token);
        });

    public override async Task<ProbeOutcome> AssertAsync(ProbeEnvironment env, FireReport fire)
    {
        await using var db = env.NewDbContext();

        var userIds = _customers.Select(c => c.UserId).ToList();
        var codes = await db.Orders
            .AsNoTracking()
            .Where(o => o.UserId != null && userIds.Contains(o.UserId.Value))
            .Select(o => o.OrderCode)
            .ToListAsync();

        var duplicates = codes.GroupBy(c => c).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        var details = new List<string>
        {
            $"Đơn tạo được: {codes.Count}/{Customers}",
            $"Mã đơn phân biệt: {codes.Distinct().Count()}",
            $"Mã HTTP: {fire.Histogram()}"
        };

        if (duplicates.Count > 0)
        {
            return ProbeOutcome.Fail(
                $"Có {duplicates.Count} OrderCode bị trùng: {string.Join(", ", duplicates.Take(5))}",
                details.ToArray());
        }

        if (fire.ServerErrors > 0)
        {
            return ProbeOutcome.Fail($"Có {fire.ServerErrors} phản hồi 5xx.", details.ToArray());
        }

        if (codes.Count != Customers)
        {
            return ProbeOutcome.Fail(
                $"Chỉ tạo được {codes.Count}/{Customers} đơn.", details.ToArray());
        }

        return ProbeOutcome.Pass($"{Customers} đơn, {codes.Distinct().Count()} mã phân biệt.",
            details.ToArray());
    }
}
