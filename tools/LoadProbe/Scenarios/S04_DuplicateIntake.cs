using Microsoft.EntityFrameworkCore;
using PBL3.Shared.DTOs.ServiceTickets;
using PBL3.Tools.LoadProbe.Infra;

namespace PBL3.Tools.LoadProbe.Scenarios;

/// <summary>
/// 10 lần tiếp nhận cùng MỘT serial, bắn đồng thời.
///
/// Chốt hiện tại là HasOpenTicketForSerialAsync — một phép check-then-act. Hai
/// request đọc "chưa có phiếu nào" trong cùng một khoảnh khắc thì cả hai đều tạo
/// phiếu, và cửa hàng có hai phiếu sửa chữa cho một cái máy.
/// </summary>
public sealed class S04_DuplicateIntake : ScenarioBase
{
    private const int Attempts = 10;

    private int _serialId;
    private string _serialNumber = string.Empty;

    public override string Id => "S04";
    public override string Title => "10 lần tiếp nhận cùng một serial";
    public override string Invariant => "Đúng 1 phiếu dịch vụ chưa đóng cho serial đó";
    public override int ExpectedRequests => Attempts;

    protected override async Task SetupCoreAsync(ProbeEnvironment env)
    {
        await Fixture.EnsureCatalogAsync(1);
        var sold = await Fixture.CreateSoldSerialAsync();
        _serialId = sold.SerialId;
        _serialNumber = sold.SerialNumber;
    }

    public override Task<FireReport> FireAsync(ProbeEnvironment env)
        => ConcurrentFire.FireAsync(Attempts, _ =>
            env.PostAsync("api/service-tickets", new ServiceTicketIntakeRequestDto
            {
                SerialNumber = _serialNumber,
                CustomerReportedIssue = "LoadProbe S04 — máy không lên nguồn",
                CosmeticNotes = "LoadProbe"
            }, Fixture.AdminToken));

    public override async Task<ProbeOutcome> AssertAsync(ProbeEnvironment env, FireReport fire)
    {
        await using var db = env.NewDbContext();

        // "Chưa đóng" = chưa Completed (9) và chưa Cancelled (10).
        var openTickets = await db.ServiceTickets
            .AsNoTracking()
            .Where(t => t.SerialId == _serialId && t.Status != 9 && t.Status != 10)
            .Select(t => t.TicketCode)
            .ToListAsync();

        var details = new[]
        {
            $"Phiếu chưa đóng cho serial: {openTickets.Count} ({string.Join(", ", openTickets.Take(5))})",
            $"Mã HTTP: {fire.Histogram()}"
        };

        if (openTickets.Count > 1)
        {
            return ProbeOutcome.Fail(
                $"Một serial có {openTickets.Count} phiếu chưa đóng.", details);
        }

        if (openTickets.Count == 0)
        {
            return ProbeOutcome.Inconclusive(
                "Không phiếu nào được tạo — kiểm lại serial có ở trạng thái Sold không.", details);
        }

        return ProbeOutcome.Pass("Đúng 1 phiếu chưa đóng.", details);
    }
}
