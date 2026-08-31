using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Shared.DTOs.ServiceTickets;
using PBL3.Tools.LoadProbe.Infra;

namespace PBL3.Tools.LoadProbe.Scenarios;

/// <summary>
/// Hai lần lập báo giá song song trên cùng một phiếu.
///
/// Hai báo giá cùng Pending trên một phiếu là trạng thái không hợp lệ về nghiệp
/// vụ: khách duyệt cái nào cũng được, và cái còn lại vĩnh viễn treo ở Pending.
/// </summary>
public sealed class S08_CreateQuotationRace : ScenarioBase
{
    private const int Attempts = 2;

    private int _ticketId;

    public override string Id => "S08";
    public override string Title => "2 lần lập báo giá song song trên một phiếu";
    public override string Invariant => "Đúng 1 báo giá ở trạng thái Pending (0)";
    public override int ExpectedRequests => Attempts;

    protected override async Task SetupCoreAsync(ProbeEnvironment env)
    {
        await Fixture.EnsureCatalogAsync(1);
        var sold = await Fixture.CreateSoldSerialAsync();

        await using var db = env.NewDbContext();

        var ticket = new ServiceTicket
        {
            TicketCode = $"LP-ST-{Guid.NewGuid():N}"[..14],
            SerialId = sold.SerialId,
            OriginalOrderId = sold.OrderId,
            CustomerId = sold.CustomerId,
            IntakeDate = DateTime.UtcNow,
            IntakeEmployeeId = Fixture.AdminUserId,
            AssignedEmployeeId = Fixture.AdminUserId,
            CustomerReportedIssue = "LoadProbe S08",
            Status = 1,          // Diagnosing — điều kiện để lập báo giá
            ResolutionType = 4,  // PaidRepair — chỉ nhánh sửa tính phí mới có báo giá
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow
        };
        db.ServiceTickets.Add(ticket);
        await db.SaveChangesAsync();
        _ticketId = ticket.Id;
    }

    public override Task<FireReport> FireAsync(ProbeEnvironment env)
        => ConcurrentFire.FireAsync(Attempts, _ =>
            env.PostAsync($"api/service-tickets/{_ticketId}/quotation", new QuotationCreateDto
            {
                LaborCost = 150_000m,
                Items = new List<QuotationItemCreateDto>()
            }, Fixture.AdminToken));

    public override async Task<ProbeOutcome> AssertAsync(ProbeEnvironment env, FireReport fire)
    {
        await using var db = env.NewDbContext();

        var all = await db.Quotations
            .AsNoTracking()
            .Where(q => q.TicketId == _ticketId)
            .Select(q => q.Status)
            .ToListAsync();

        var pending = all.Count(s => s == 0);

        var details = new[]
        {
            $"Tổng báo giá trên phiếu: {all.Count}",
            $"Báo giá đang Pending: {pending} (kỳ vọng 1)",
            $"Mã HTTP: {fire.Histogram()}"
        };

        if (pending > 1)
        {
            return ProbeOutcome.Fail(
                $"Phiếu có {pending} báo giá cùng ở trạng thái Pending.", details);
        }

        if (pending == 0)
        {
            return ProbeOutcome.Inconclusive(
                "Không báo giá nào được lập — kiểm lại phiếu có ở trạng thái Diagnosing/PaidRepair không.",
                details);
        }

        return ProbeOutcome.Pass("Đúng 1 báo giá Pending.", details);
    }
}
