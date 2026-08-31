using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PBL3.Shared.DTOs.Auth;
using PBL3.Shared.DTOs.Common;
using PBL3.Tools.LoadProbe.Infra;

namespace PBL3.Tools.LoadProbe.Scenarios;

/// <summary>
/// Hai lời gọi refresh-token song song với CÙNG một cặp token.
///
/// Bối cảnh thật: trang vừa tải, `JwtAuthenticationStateProvider` và
/// `AuthHeaderHandler` cùng phát hiện token hết hạn. Rotation lưu hash của token
/// MỚI đè lên hash cũ, không có điều kiện nào — nên nếu cả hai cùng thành công thì
/// một trong hai client cầm một refresh token đã chết ngay từ lúc nhận được, và
/// người dùng bị đăng xuất oan mà không ai thấy lỗi.
///
/// Bất biến: đúng MỘT trong hai lời gọi được chấp nhận, và hash lưu trong DB phải
/// khớp với refresh token đã trả cho chính lời gọi đó.
/// </summary>
public sealed class S09_RefreshTokenRace : ScenarioBase
{
    private ProbeCustomer _customer = null!;
    private string _plainRefreshToken = string.Empty;
    private readonly List<TokenResponse> _issued = new();

    public override string Id => "S09";
    public override string Title => "2 lời gọi refresh-token cùng một cặp";
    public override string Invariant =>
        "Đúng 1 lời gọi thành công, và hash trong DB khớp token đã trả cho lời gọi đó";
    public override int ExpectedRequests => 2;

    protected override async Task SetupCoreAsync(ProbeEnvironment env)
    {
        _customer = (await Fixture.CreateCustomersAsync(1)).Single();

        // Gieo thẳng cặp refresh token vào DB thay vì đăng nhập: /api/auth/login bị
        // giới hạn 5 lần/phút mỗi IP, và refresh-token 10 lần/phút — đốt suất đăng
        // nhập ở pha seed là tự bóp hạn mức của chính pha bắn.
        _plainRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var hashed = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(_plainRefreshToken)));

        await using var db = env.NewDbContext();
        await db.Users
            .Where(u => u.Id == _customer.UserId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.RefreshToken, hashed)
                .SetProperty(u => u.RefreshTokenExpiryTime, DateTime.UtcNow.AddDays(7)));
    }

    public override async Task<FireReport> FireAsync(ProbeEnvironment env)
    {
        var report = new FireReport();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var body = new RefreshTokenRequest
        {
            AccessToken = _customer.Token,
            RefreshToken = _plainRefreshToken
        };

        var tasks = Enumerable.Range(0, 2).Select(async _ =>
        {
            await gate.Task;
            try
            {
                using var response = await env.PostAsync("api/auth/refresh-token", body, null);
                report.Record(response.StatusCode);

                if (!response.IsSuccessStatusCode) return;

                var payload = await response.Content.ReadFromJsonAsync<ApiResult<TokenResponse>>();
                if (payload is { Success: true, Data: not null })
                {
                    lock (_issued) _issued.Add(payload.Data);
                }
            }
            catch (Exception ex)
            {
                report.RecordTransportError(ex.GetBaseException().Message);
            }
        }).ToArray();

        gate.SetResult();
        await Task.WhenAll(tasks);
        return report;
    }

    public override async Task<ProbeOutcome> AssertAsync(ProbeEnvironment env, FireReport fire)
    {
        await using var db = env.NewDbContext();

        var storedHash = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == _customer.UserId)
            .Select(u => u.RefreshToken)
            .FirstAsync();

        var issuedHashes = _issued
            .Select(t => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(t.RefreshToken))))
            .ToList();

        var stranded = issuedHashes.Count(h => h != storedHash);

        var details = new[]
        {
            $"Số cặp token được cấp: {_issued.Count} (kỳ vọng 1)",
            $"Số client cầm refresh token đã chết ngay lúc nhận: {stranded} (kỳ vọng 0)",
            $"Mã HTTP: {fire.Histogram()}"
        };

        if (_issued.Count == 0)
        {
            return ProbeOutcome.Inconclusive(
                "Không lời gọi nào thành công — kiểm lại hash refresh token đã gieo đúng chưa.",
                details);
        }

        if (stranded > 0)
        {
            return ProbeOutcome.Fail(
                $"{stranded} client nhận được refresh token đã bị vô hiệu ngay lúc cấp " +
                "— lần refresh kế tiếp của họ sẽ bị đá về trang đăng nhập.", details);
        }

        return ProbeOutcome.Pass("Đúng một lời gọi được chấp nhận, không ai bị đăng xuất oan.", details);
    }
}
