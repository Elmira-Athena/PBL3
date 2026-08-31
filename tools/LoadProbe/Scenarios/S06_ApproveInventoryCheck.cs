using Microsoft.EntityFrameworkCore;
using PBL3.Tools.LoadProbe.Infra;

namespace PBL3.Tools.LoadProbe.Scenarios;

/// <summary>
/// 5 lần phê duyệt cùng một phiếu kiểm kê, bắn đồng thời.
///
/// Bất biến ở đây là SỔ TỔN THẤT, không phải trạng thái phiếu: mỗi serial thất
/// thoát được phép sinh ĐÚNG một bản ghi InventoryAdjustmentLog. Nhân đôi bản ghi
/// là nhân đôi chi phí tổn thất trong báo cáo tài chính — và đây chính là bảng mà
/// đợt 3 định thêm unique index (AuditCheckId, SerialId).
/// </summary>
public sealed class S06_ApproveInventoryCheck : ScenarioBase
{
    private const int Attempts = 5;

    private int _checkId;
    private int _serialId;

    public override string Id => "S06";
    public override string Title => "5 lần phê duyệt cùng một phiếu kiểm kê";
    public override string Invariant =>
        "InventoryAdjustmentLogs không nhân đôi: đúng 1 bản ghi cho mỗi serial";
    public override int ExpectedRequests => Attempts;

    protected override async Task SetupCoreAsync(ProbeEnvironment env)
    {
        await Fixture.EnsureCatalogAsync(1);
        var serial = await Fixture.CreateAvailableSerialAsync();
        _serialId = serial.SerialId;
        _checkId = await Fixture.CreateAwaitingApprovalCheckAsync(serial.SerialId, serial.SerialNumber);
    }

    public override Task<FireReport> FireAsync(ProbeEnvironment env)
        => ConcurrentFire.FireAsync(Attempts, _ =>
            env.PostEmptyAsync($"api/inventory-checks/{_checkId}/approve", Fixture.AdminToken));

    public override async Task<ProbeOutcome> AssertAsync(ProbeEnvironment env, FireReport fire)
    {
        await using var db = env.NewDbContext();

        var logs = await db.InventoryAdjustmentLogs
            .AsNoTracking()
            .CountAsync(l => l.AuditCheckId == _checkId && l.SerialId == _serialId);

        var serialStatus = await db.ProductSerials
            .AsNoTracking()
            .Where(s => s.Id == _serialId)
            .Select(s => s.Status)
            .FirstAsync();

        var checkStatus = await db.InventoryChecks
            .AsNoTracking()
            .Where(c => c.Id == _checkId)
            .Select(c => c.Status)
            .FirstAsync();

        var details = new[]
        {
            $"InventoryAdjustmentLogs cho (phiếu, serial) = {logs} (kỳ vọng 1)",
            $"ProductSerial.Status = {serialStatus} (kỳ vọng 5 = Lost)",
            $"InventoryCheck.Status = {checkStatus} (kỳ vọng 2 = Completed)",
            $"Mã HTTP: {fire.Histogram()}"
        };

        if (logs > 1)
        {
            return ProbeOutcome.Fail(
                $"Sổ tổn thất có {logs} bản ghi cho cùng một serial trong cùng một phiếu.", details);
        }

        if (logs == 0)
        {
            return ProbeOutcome.Inconclusive(
                "Không bản ghi điều chỉnh nào — phiếu có thể chưa được duyệt lần nào.", details);
        }

        return ProbeOutcome.Pass("Đúng 1 bản ghi điều chỉnh.", details);
    }
}
