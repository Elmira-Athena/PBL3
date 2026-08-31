using Microsoft.EntityFrameworkCore;
using PBL3.Shared.DTOs.Pos;
using PBL3.Tools.LoadProbe.Infra;

namespace PBL3.Tools.LoadProbe.Scenarios;

/// <summary>
/// POS bán serial S trong khi phê duyệt kiểm kê đang đánh chính S là thất thoát.
///
/// Kịch bản nguy hiểm nhất trong bộ này, vì hậu quả là GHI ĐÈ IM LẶNG: EF sinh
/// UPDATE ProductSerials SET Status = 5 WHERE Id = @p, không có mệnh đề trạng thái
/// nào. Một serial khách vừa mua bị ghi đè thành "thất thoát", cả hai request đều
/// trả 200, và không có cách nào phát hiện ngoài việc đối chiếu bảng.
///
/// Đây đúng là ca mà RowVersion ở đợt 3 sinh ra để chặn.
/// </summary>
public sealed class S07_PosVersusInventoryLoss : ScenarioBase
{
    private int _checkId;
    private int _serialId;

    public override string Id => "S07";
    public override string Title => "POS bán serial S xen kẽ kiểm kê đánh S thất thoát";
    public override string Invariant => "Nếu serial đã bán thì Status phải khác Lost (5)";
    public override int ExpectedRequests => 2;

    protected override async Task SetupCoreAsync(ProbeEnvironment env)
    {
        await Fixture.EnsureCatalogAsync(1);
        var serial = await Fixture.CreateAvailableSerialAsync();
        _serialId = serial.SerialId;
        _checkId = await Fixture.CreateAwaitingApprovalCheckAsync(serial.SerialId, serial.SerialNumber);
    }

    public override async Task<FireReport> FireAsync(ProbeEnvironment env)
    {
        var (pos, approve) = await ConcurrentFire.FireInterleavedAsync(
            1, _ => env.PostAsync("api/pos/checkout", new PosCheckoutRequest
            {
                PaymentMethod = 0,
                EmployeeNote = "LoadProbe S07",
                Items = new List<PosCheckoutItemRequest>
                {
                    new() { SerialId = _serialId, VariantId = Fixture.VariantId, Quantity = 1 }
                }
            }, Fixture.AdminToken),
            1, _ => env.PostEmptyAsync($"api/inventory-checks/{_checkId}/approve", Fixture.AdminToken));

        // Gộp hai thống kê lại để lớp phát hiện phép đo rỗng nhìn thấy cả hai phía.
        var merged = new FireReport();
        merged.MergeFrom(pos);
        merged.MergeFrom(approve);
        return merged;
    }

    public override async Task<ProbeOutcome> AssertAsync(ProbeEnvironment env, FireReport fire)
    {
        await using var db = env.NewDbContext();

        var status = await db.ProductSerials
            .AsNoTracking()
            .Where(s => s.Id == _serialId)
            .Select(s => s.Status)
            .FirstAsync();

        var soldToCustomer = await db.Set<PBL3.Core.Entities.OrderSerial>()
            .AsNoTracking()
            .AnyAsync(os => os.SerialId == _serialId);

        var details = new[]
        {
            $"ProductSerial.Status = {status}",
            $"Đã gắn vào đơn bán (OrderSerials): {(soldToCustomer ? "có" : "không")}",
            $"Mã HTTP: {fire.Histogram()}"
        };

        if (soldToCustomer && status == 5)
        {
            return ProbeOutcome.Fail(
                "Serial đã bán cho khách nhưng bị kiểm kê ghi đè thành Lost (5).", details);
        }

        if (!soldToCustomer && status != 5)
        {
            return ProbeOutcome.Inconclusive(
                $"Không bán được cũng không đánh thất thoát (Status = {status}) — " +
                "cả hai request có thể đều thất bại, chưa đo được gì.", details);
        }

        return ProbeOutcome.Pass(
            soldToCustomer
                ? $"Serial đã bán và giữ trạng thái {status} (không bị ghi đè)."
                : "Serial không bán được và được ghi nhận thất thoát — nhất quán.",
            details);
    }
}
