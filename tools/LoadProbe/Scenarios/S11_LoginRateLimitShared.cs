using Microsoft.EntityFrameworkCore;
using Npgsql;
using PBL3.Shared.DTOs.Auth;
using PBL3.Tools.LoadProbe.Infra;

namespace PBL3.Tools.LoadProbe.Scenarios;

/// <summary>
/// 20 lời gọi đăng nhập song song từ CÙNG một IP, với hạn mức 5 request/phút.
///
/// Bối cảnh thật: bộ đếm rate limit từng nằm trong RAM của MỘT tiến trình
/// (<c>System.Threading.RateLimiting</c>). Với một task thì đúng; với N task sau ALB thì
/// mỗi task đếm riêng và mọi hạn mức nhân N — "5 lần/phút" thành 5N. Đo được ở bản cũ với
/// 2 replica: 10 request đi qua thay vì 5. Đó là lý do <c>RateLimitCounters</c> +
/// <c>IRateLimitStore</c> ra đời, và kịch bản này là thứ chứng minh chúng có hiệu lực.
///
/// Bất biến: đúng 5 request đi qua limiter, 15 request nhận 429, VÀ đúng MỘT hàng
/// <c>RateLimitCounters</c> với <c>Count = 20</c> — bất kể API đang chạy mấy task.
///
/// 🎯 <b>Vì sao phải khẳng định CẢ HAI vế, không chỉ mã HTTP.</b> "5 đi qua, 15 ăn 429" là
/// điều <b>bản cũ trong RAM cũng làm được</b> khi chỉ có một task — nên riêng mã HTTP không
/// phân biệt được "bộ đếm dùng chung" với "bộ đếm riêng, tình cờ chỉ có một tiến trình".
/// Thứ phân biệt được là <b>một hàng duy nhất trong DB đếm đủ 20</b>: nó nói rằng cả 20
/// request, dù task nào phục vụ, đều đi qua đúng một ô đếm. Ngược lại, mã HTTP cũng là phần
/// thật của bất biến ở đây (429 là hợp đồng với client, kèm <c>Retry-After</c>), nên bỏ vế
/// HTTP mà chỉ xem <c>Count</c> thì bỏ luôn câu hỏi "có thật là bị chặn không".
///
/// 🚨 <b>ĐỪNG TĂNG 20 LÊN.</b> <c>GlobalLimiter</c> (100 request/10 giây mỗi IP,
/// <c>src/API/Program.cs</c>) vẫn còn và vẫn nằm TRONG RAM — nó cố ý không đi qua bảng này
/// vì nó chạm mọi request duyệt catalogue. 20 request thì chưa chạm nó; vượt 100 thì 429 đến
/// từ HAI nguồn khác nhau, mà ở tầng HTTP hai loại 429 <b>giống nhau như đúc</b>. Lúc đó
/// phép đo mất nghĩa: không nói được request bị chặn bởi hạn mức đang đo hay bởi trần chung
/// per-instance. (Vế <c>Count</c> ở DB vẫn cứu được một phần — nó chỉ đếm request tới được
/// action filter — nhưng đừng dựa vào đó, hãy giữ burst dưới trần chung.)
/// </summary>
public sealed class S11_LoginRateLimitShared : ScenarioBase
{
    /// <summary>
    /// Ba hằng số này phải KHỚP với cấu hình thật của endpoint đăng nhập
    /// (<c>src/API/Controllers/Storefront/AuthController.cs</c> + <c>src/API/Program.cs</c>).
    /// Lệch một cái là kịch bản đo một hạn mức không tồn tại rồi báo HỎNG oan.
    /// </summary>
    private const string PolicyName = "LoginRateLimit";

    private const int PermitLimit = 5;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    /// <summary>Tiền tố khoá phân vùng — <c>DbRateLimitFilter</c> ghép "&lt;policy&gt;:&lt;IP&gt;".</summary>
    private const string PartitionPrefix = PolicyName + ":";

    private const int BurstSize = 20;

    /// <summary>
    /// Nếu cửa sổ hiện tại còn ít hơn số giây này thì chờ sang cửa sổ kế. Không phải cẩn thận
    /// thái quá: 20 request rơi vào HAI cửa sổ sẽ có HAI ô đếm, mỗi ô cấp 5 suất ⇒ 10 request
    /// đi qua một hạn mức 5 mà KHÔNG có lỗi nào — trông y hệt lỗi "mỗi task đếm riêng" đang
    /// cần đo. Nhầm hai thứ đó là kết luận sai theo hướng tệ nhất.
    /// </summary>
    private const int MinHeadroomSeconds = 20;

    private ProbeCustomer _customer = null!;
    private DateTime _windowStartBefore;
    private DateTime _windowStartAfter;

    public override string Id => "S11";
    public override string Title => "20 lần đăng nhập song song từ cùng một IP";

    public override string Invariant =>
        "Đúng 5 request đi qua limiter, 15 nhận 429, và MỘT hàng RateLimitCounters có Count = 20";

    public override int ExpectedRequests => BurstSize;

    /// <summary>
    /// 429 ở đây là KẾT QUẢ MONG ĐỢI, không phải dấu hiệu phép đo rỗng — xem ghi chú ở
    /// <see cref="IProbeScenario.RateLimitIsUnderTest"/>.
    /// </summary>
    public override bool RateLimitIsUnderTest => true;

    protected override async Task SetupCoreAsync(ProbeEnvironment env)
    {
        // Một tài khoản THẬT, cố tình đăng nhập bằng mật khẩu sai. Không cần mật khẩu đúng:
        // limiter chặn TRƯỚC khi biết mật khẩu, nên request "đi qua" hay không không phụ
        // thuộc kết quả xác thực.
        //
        // Khách do ProbeFixture tạo có LockoutEnabled = false, nên 5 lần sai không kích hoạt
        // lockout của Identity — nếu có, lần chạy lại sẽ nhận một câu trả lời khác cho cùng
        // mã HTTP và phép đo bị nhiễu bởi một cơ chế thứ hai.
        _customer = (await Fixture.CreateCustomersAsync(1)).Single();

        // Chờ cho cửa sổ hiện tại còn đủ chỗ. Xem MinHeadroomSeconds.
        var now = DateTime.UtcNow;
        var remaining = FloorToWindow(now) + Window - now;
        if (remaining < TimeSpan.FromSeconds(MinHeadroomSeconds))
        {
            Console.WriteLine(
                $"   … chờ {remaining.TotalSeconds:F0}s cho cửa sổ {Window.TotalSeconds:F0}s "
                + "lăn sang mốc mới (tránh burst bị chẻ làm hai ô đếm)");
            await Task.Delay(remaining + TimeSpan.FromMilliseconds(500));
        }
    }

    public override async Task<FireReport> FireAsync(ProbeEnvironment env)
    {
        // ⚠️ THÂN REQUEST PHẢI HỢP LỆ VỚI VALIDATOR, nếu không phép đo đổi chủ đề.
        // `DbRateLimitFilter` là action filter (Order 0), còn ModelState 400 tự động của
        // [ApiController] là một action filter chạy TRƯỚC (Order -2000). Body sai định dạng
        // ⇒ 400 mà KHÔNG được tính vào ô đếm, và ta sẽ đo validator chứ không đo limiter.
        // Vì vậy: email đúng định dạng, mật khẩu không rỗng — chỉ sai nội dung.
        var body = new LoginRequest
        {
            Email = _customer.Email,
            Password = "mat-khau-sai-co-y-S11"
        };

        _windowStartBefore = FloorToWindow(DateTime.UtcNow);

        var report = await ConcurrentFire.FireAsync(
            BurstSize, _ => env.PostAsync("api/auth/login", body, null));

        // Đọc lại mốc cửa sổ NGAY sau loạt bắn. Nếu nó đã đổi thì burst vắt qua ranh giới và
        // mọi con số phía sau đều không so được với kỳ vọng — AssertAsync sẽ trả KHÔNG KẾT LUẬN.
        _windowStartAfter = FloorToWindow(DateTime.UtcNow);

        return report;
    }

    public override async Task<ProbeOutcome> AssertAsync(ProbeEnvironment env, FireReport fire)
    {
        // "Đi qua limiter" = mọi thứ không phải 429 và không phải lỗi mạng. Cố ý KHÔNG dùng
        // fire.Successes: request đi qua vẫn trả 400 ("Tài khoản hoặc mật khẩu không đúng"),
        // nên đếm 2xx sẽ ra 0 và báo HỎNG oan.
        var passed = fire.Total - fire.RateLimited - fire.TransportErrors.Count;

        var details = new List<string>
        {
            $"Request đi qua limiter: {passed} (kỳ vọng {PermitLimit})",
            $"Request bị chặn 429: {fire.RateLimited} (kỳ vọng {BurstSize - PermitLimit})",
            $"Mã HTTP: {fire.Histogram()}",
            $"Cửa sổ đo: mốc {_windowStartBefore:yyyy-MM-dd HH:mm:ss}Z, "
            + $"dài {Window.TotalSeconds:F0}s, hạn mức {PermitLimit}"
        };

        if (fire.ServerErrors > 0)
        {
            // Không làm bất biến sai — 500 vẫn là "đã đi qua limiter" — nhưng phải nói ra.
            details.Add($"⚠️ {fire.ServerErrors} request trả 5xx: chúng ĐÃ đi qua limiter, " +
                        "nhưng hãy soi log API xem có phải store fail-open (Degraded) không.");
        }

        if (_windowStartBefore != _windowStartAfter)
        {
            return ProbeOutcome.Inconclusive(
                "Loạt bắn vắt qua ranh giới cửa sổ nên có hai ô đếm, mỗi ô cấp một hạn mức " +
                "riêng. Chạy lại — kịch bản tự canh mốc trước khi bắn.",
                details.ToArray());
        }

        List<CounterRow> rows;
        try
        {
            await using var db = env.NewDbContext();

            // StartsWith (⇒ LIKE 'LoginRateLimit:%') chứ không ILike: đây là cột vận hành,
            // khoá do CHÍNH server ghép nên chữ hoa/thường cố định, và tiền tố không chứa
            // '%' hay '_' nên không có chuyện escape như luật ô tìm kiếm của CLAUDE.md.
            //
            // Lọc theo WindowStart >= mốc đã đo để không đếm lẫn ô của cửa sổ trước.
            rows = await db.RateLimitCounters
                .AsNoTracking()
                .Where(c => c.PartitionKey.StartsWith(PartitionPrefix)
                            && c.WindowStart >= _windowStartBefore)
                .OrderBy(c => c.WindowStart)
                .Select(c => new CounterRow(c.PartitionKey, c.WindowStart, c.Count))
                .ToListAsync();
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            // 42P01 = undefined_table. Nhận diện bằng SqlState, không dò ex.Message — chuỗi đó
            // tiếng Anh và đổi theo phiên bản (cùng lý lẽ với ConflictClassifier).
            return ProbeOutcome.Inconclusive(
                "Bảng RateLimitCounters không tồn tại trong DB — migration cho bộ đếm dùng " +
                "chung chưa được sinh/áp. Không kết luận được gì về hạn mức.",
                details.ToArray());
        }

        details.Add(rows.Count == 0
            ? "Hàng RateLimitCounters khớp cửa sổ: KHÔNG CÓ"
            : "Hàng RateLimitCounters khớp cửa sổ: " +
              string.Join(" | ", rows.Select(r => $"{r.PartitionKey} → Count={r.Count}")));

        // ── Bốn cửa KHÔNG KẾT LUẬN, tất cả đứng TRƯỚC mọi kết luận ĐẠT ────────────────
        //
        // Luật của repo: KHÔNG KẾT LUẬN ≠ ĐẠT. Một ✅ ở đây mà thực ra bộ đếm chưa nằm trên
        // đường đăng nhập là bằng chứng giả — tệ hơn không có bằng chứng, vì nó sẽ được
        // trích dẫn để bật max_instance_count > 1.

        if (rows.Count == 0)
        {
            return ProbeOutcome.Inconclusive(
                $"Không có hàng đếm nào cho '{PartitionPrefix}…' — endpoint đăng nhập KHÔNG " +
                "đi qua IRateLimitStore (còn [EnableRateLimiting] in-process, hoặc " +
                "[DbRateLimit] chưa gắn, hoặc IRateLimitStore chưa đăng ký DI). Mã HTTP có " +
                "thể vẫn đúng với 1 task, nên phép đo này KHÔNG chứng minh được điều gì về " +
                "nhiều task.",
                details.ToArray());
        }

        if (rows.Count > 1)
        {
            return ProbeOutcome.Inconclusive(
                $"Tìm thấy {rows.Count} ô đếm khác nhau cho cùng loạt bắn — probe bị API " +
                "thấy dưới nhiều IP (proxy/IPv4 lẫn IPv6), hoặc cửa sổ đã lăn. Mỗi ô cấp " +
                "một hạn mức riêng nên không so được với kỳ vọng.",
                details.ToArray());
        }

        var counted = rows[0].Count;

        if (passed == 0)
        {
            return ProbeOutcome.Inconclusive(
                $"Cả {BurstSize} request đều bị chặn — hạn mức của cửa sổ này đã bị đốt hết " +
                $"trước phép đo (lần chạy trước cách đây dưới {Window.TotalSeconds:F0}s, hoặc " +
                "GlobalLimiter 100/10s chen vào). Chờ hết cửa sổ rồi đo lại.",
                details.ToArray());
        }

        // ── Bất biến ──────────────────────────────────────────────────────────────────

        if (passed > PermitLimit)
        {
            return ProbeOutcome.Fail(
                $"{passed} request đi qua một hạn mức {PermitLimit} — bộ đếm KHÔNG dùng " +
                $"chung. Tỉ lệ {(double)passed / PermitLimit:F1}× thường bằng đúng số task " +
                "API đang chạy (mỗi task đếm riêng 5).",
                details.ToArray());
        }

        if (counted > BurstSize)
        {
            return ProbeOutcome.Inconclusive(
                $"Ô đếm có Count = {counted} > {BurstSize} request vừa bắn, tức nó đã mang " +
                "số dư từ trước phép đo. Chờ hết cửa sổ rồi đo lại.",
                details.ToArray());
        }

        if (counted < BurstSize)
        {
            return ProbeOutcome.Fail(
                $"Chỉ {counted}/{BurstSize} request được tính vào ô đếm dùng chung. Số còn " +
                "lại đi qua một đường KHÔNG chạm bảng này (bộ đếm in-process, hay store " +
                "fail-open vì DB không tới được) — với nhiều task nghĩa là hạn mức đã nhân lên.",
                details.ToArray());
        }

        if (fire.RateLimited != BurstSize - PermitLimit || passed < PermitLimit)
        {
            return ProbeOutcome.Fail(
                $"Ô đếm đúng ({counted}) nhưng số request đi qua là {passed}, không phải " +
                $"{PermitLimit} — limiter chặn quá tay, người dùng hợp lệ bị từ chối oan.",
                details.ToArray());
        }

        return ProbeOutcome.Pass(
            $"Đúng {PermitLimit} request đi qua và {fire.RateLimited} bị chặn 429, cả " +
            $"{counted} request nằm trong MỘT ô đếm dùng chung.",
            details.ToArray());
    }

    private sealed record CounterRow(string PartitionKey, DateTime WindowStart, int Count);

    /// <summary>
    /// Bản sao của <c>PostgresRateLimitStore.FloorToWindow</c> (hàm đó là <c>internal</c> nên
    /// không gọi được từ đây). PHẢI giữ giống hệt công thức bên đó: lệch công thức là probe
    /// đi tìm ô đếm ở một mốc mà server không bao giờ ghi vào, và kịch bản luôn ra
    /// "không có hàng đếm nào".
    /// </summary>
    private static DateTime FloorToWindow(DateTime utcNow)
        => new(utcNow.Ticks / Window.Ticks * Window.Ticks, DateTimeKind.Utc);
}
