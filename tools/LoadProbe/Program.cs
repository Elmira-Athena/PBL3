using System.Diagnostics;
using PBL3.Tools.LoadProbe.Infra;
using PBL3.Tools.LoadProbe.Scenarios;

// ═══════════════════════════════════════════════════════════════════════════════
// LoadProbe — bộ đo tính đúng đắn dưới tải đồng thời
//
// Không phải công cụ đo hiệu năng. Nó bắn request song song rồi KHẲNG ĐỊNH BẤT
// BIẾN bằng LINQ trên DB. Lý do: mọi lỗi đúng đắn dữ liệu đã tìm thấy ở repo này
// đều trả HTTP 200. Đếm mã lỗi không phát hiện được cái nào trong số đó.
//
// Cách chạy: xem tools/LoadProbe/README.md
// ═══════════════════════════════════════════════════════════════════════════════

ProbeConfig config;
try
{
    config = ProbeConfig.Parse(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine("Cấu hình không hợp lệ: " + ex.Message);
    return 3;
}

using var env = new ProbeEnvironment(config);

if (!await env.IsApiUpAsync())
{
    Console.Error.WriteLine(
        $"Không gọi được {config.ApiBaseUrl}/health/live. Khởi động API trước khi đo — " +
        "xem docs/bat-dau-phien-moi.md mục 3.");
    return 3;
}

var fixture = new ProbeFixture(env);
try
{
    await fixture.InitializeAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine("Không khởi tạo được dữ liệu nền: " + ex.Message);
    return 3;
}

var all = new List<IProbeScenario>
{
    new S01_ConcurrentCheckout(),
    new S02_VoucherQuantityOne(),
    new S03_VoucherPerUserLimit(),
    new S04_DuplicateIntake(),
    new S05_AcceptQuotationRace(),
    new S06_ApproveInventoryCheck(),
    new S07_PosVersusInventoryLoss(),
    new S08_CreateQuotationRace(),
    new S09_RefreshTokenRace()
};

var selected = config.ScenarioIds.Count == 0
    ? all
    : all.Where(s => config.ScenarioIds.Contains(s.Id)).ToList();

if (selected.Count == 0)
{
    Console.Error.WriteLine("Không kịch bản nào khớp --scenarios. Mã hợp lệ: " +
                            string.Join(", ", all.Select(s => s.Id)));
    return 3;
}

// Dọn tàn dư của lần chạy trước. Bắt buộc: một phiếu kiểm kê "Chờ duyệt" còn sót
// lại từ lần chạy bị ngắt giữa chừng sẽ làm kịch bản sau đọc nhầm số liệu.
Console.WriteLine("Dọn dữ liệu probe còn sót từ lần chạy trước…");
await fixture.CleanupAsync();

var startedAt = DateTime.Now;
var reports = new List<ScenarioReport>();

for (var i = 0; i < selected.Count; i++)
{
    var scenario = selected[i];
    var report = new ScenarioReport
    {
        Id = scenario.Id,
        Title = scenario.Title,
        Invariant = scenario.Invariant
    };
    reports.Add(report);

    Console.WriteLine();
    Console.WriteLine($"── {scenario.Id}: {scenario.Title}");

    var stopwatch = Stopwatch.StartNew();
    try
    {
        await scenario.SetupAsync(env, fixture);
        var fire = await scenario.FireAsync(env);
        report.Fire = fire;

        // Cửa chặn phép đo rỗng, chạy TRƯỚC phần khẳng định bất biến. Một kịch bản
        // mà 49/50 request ăn 429 vẫn thoả mọi bất biến — vì code cần đo chưa chạy.
        var vacuity = fire.VacuityReason(scenario.ExpectedRequests);
        report.Outcome = vacuity is not null
            ? ProbeOutcome.Inconclusive(vacuity, $"Mã HTTP: {fire.Histogram()}")
            : await scenario.AssertAsync(env, fire);
    }
    catch (Exception ex)
    {
        report.Outcome = ProbeOutcome.Inconclusive("Kịch bản ném ngoại lệ: " + ex.GetBaseException().Message);
        report.Error = ex.ToString();
    }
    finally
    {
        stopwatch.Stop();
        report.Elapsed = stopwatch.Elapsed;
    }

    Console.WriteLine($"   {MarkdownReporter.Verdict(report.Outcome.Verdict)} — {report.Outcome.Message}");
    foreach (var d in report.Outcome.Details) Console.WriteLine($"     · {d}");
    foreach (var sample in report.Fire?.FailureSamples ?? Array.Empty<string>())
    {
        Console.WriteLine($"     ↳ mẫu phản hồi lỗi: {sample}");
    }

    // Giãn cách để cửa sổ 10 giây của trần chung lăn qua trước kịch bản kế tiếp.
    if (i < selected.Count - 1 && config.PaceSeconds > 0)
    {
        Console.WriteLine($"   … chờ {config.PaceSeconds}s cho cửa sổ rate limiter lăn qua");
        await Task.Delay(TimeSpan.FromSeconds(config.PaceSeconds));
    }
}

if (config.KeepData)
{
    Console.WriteLine();
    Console.WriteLine("--keep: GIỮ LẠI dữ liệu probe trong DB. Dọn bằng cách chạy lại probe.");
}
else
{
    Console.WriteLine();
    Console.WriteLine("Dọn dữ liệu probe…");
    await fixture.CleanupAsync();
}

Directory.CreateDirectory(config.OutputDirectory);
var outputPath = Path.Combine(config.OutputDirectory, $"loadprobe-{startedAt:yyyyMMdd-HHmmss}.md");
await File.WriteAllTextAsync(outputPath, MarkdownReporter.Render(reports, config, startedAt));

Console.WriteLine();
Console.WriteLine("Báo cáo: " + outputPath);

var failed = reports.Count(r => r.Outcome.Verdict == ProbeVerdict.Fail);
var inconclusive = reports.Count(r => r.Outcome.Verdict == ProbeVerdict.Inconclusive);
Console.WriteLine($"Tổng kết: {reports.Count - failed - inconclusive} đạt, {failed} hỏng, {inconclusive} không kết luận.");

if (failed > 0) return 1;
if (inconclusive > 0) return 2;
return 0;
