using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Shared.DTOs.ServiceTickets;
using PBL3.Tools.LoadProbe.Infra;

namespace PBL3.Tools.LoadProbe.Scenarios;

/// <summary>
/// 10 lần duyệt cùng một báo giá, bắn đồng thời.
///
/// Đường này đã được đóng ở đợt 1 bằng TryDecideAsync (UPDATE … WHERE Status = 0).
/// Kịch bản giữ lại làm chốt chống hồi quy: nếu ai đó đổi nó về gán trạng thái
/// bằng Change Tracker thì bản ghi lịch sử sẽ nhân lên, còn HTTP vẫn 200 hết.
/// </summary>
public sealed class S05_AcceptQuotationRace : ScenarioBase
{
    private const int Attempts = 10;

    private int _ticketId;
    private int _quotationId;

    public override string Id => "S05";
    public override string Title => "10 lần duyệt cùng một báo giá";
    public override string Invariant =>
        "Quotation.Status = 1 và đúng 1 bản ghi lịch sử chuyển trạng thái từ 2";
    public override int ExpectedRequests => Attempts;

    protected override async Task SetupCoreAsync(ProbeEnvironment env)
    {
        await Fixture.EnsureCatalogAsync(1);
        var sold = await Fixture.CreateSoldSerialAsync();

        await using var db = env.NewDbContext();

        // Dựng thẳng phiếu ở trạng thái QuoteSent thay vì đi qua 4 lời gọi API.
        // Không phải để nhanh: mỗi lời gọi API là một suất trong hạn mức 100
        // request/10 giây, và kịch bản này cần dành trọn hạn mức cho phần bắn.
        var ticket = new ServiceTicket
        {
            TicketCode = $"LP-ST-{Guid.NewGuid():N}"[..14],
            SerialId = sold.SerialId,
            OriginalOrderId = sold.OrderId,
            CustomerId = sold.CustomerId,
            IntakeDate = DateTime.UtcNow,
            IntakeEmployeeId = Fixture.AdminUserId,
            AssignedEmployeeId = Fixture.AdminUserId,
            CustomerReportedIssue = "LoadProbe S05",
            Status = 2,          // QuoteSent
            ResolutionType = 4,  // PaidRepair
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow
        };
        db.ServiceTickets.Add(ticket);
        await db.SaveChangesAsync();
        _ticketId = ticket.Id;

        var quotation = new Quotation
        {
            TicketId = ticket.Id,
            IssuedDate = DateTime.UtcNow,
            IssuedByEmployeeId = Fixture.AdminUserId,
            LaborCost = 200_000m,
            PartsTotal = 0,
            GrandTotal = 200_000m,
            Status = 0 // Pending
        };
        db.Quotations.Add(quotation);
        await db.SaveChangesAsync();
        _quotationId = quotation.Id;
    }

    public override Task<FireReport> FireAsync(ProbeEnvironment env)
        => ConcurrentFire.FireAsync(Attempts, _ =>
            env.PostAsync(
                $"api/service-tickets/{_ticketId}/quotation/{_quotationId}/accept",
                new QuotationAcceptDto { NextStatus = 5 }, // 5 = InRepair
                Fixture.AdminToken));

    public override async Task<ProbeOutcome> AssertAsync(ProbeEnvironment env, FireReport fire)
    {
        await using var db = env.NewDbContext();

        var status = await db.Quotations
            .AsNoTracking()
            .Where(q => q.Id == _quotationId)
            .Select(q => q.Status)
            .FirstAsync();

        var historyCount = await db.ServiceTicketStatusHistories
            .AsNoTracking()
            .CountAsync(h => h.TicketId == _ticketId && h.FromStatus == 2);

        var details = new[]
        {
            $"Quotation.Status = {status} (kỳ vọng 1 = Accepted)",
            $"Bản ghi lịch sử từ trạng thái 2: {historyCount} (kỳ vọng 1)",
            $"Mã HTTP: {fire.Histogram()}"
        };

        if (status != 1)
        {
            return ProbeOutcome.Fail($"Báo giá không ở trạng thái Accepted (đang là {status}).", details);
        }

        if (historyCount != 1)
        {
            return ProbeOutcome.Fail(
                $"Có {historyCount} bản ghi lịch sử cho một lần duyệt.", details);
        }

        return ProbeOutcome.Pass("Đúng một lần duyệt được ghi nhận.", details);
    }
}
