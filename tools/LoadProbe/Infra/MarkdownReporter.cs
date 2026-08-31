using System.Text;

namespace PBL3.Tools.LoadProbe.Infra;

public static class MarkdownReporter
{
    public static string Render(IReadOnlyList<ScenarioReport> reports, ProbeConfig config, DateTime startedAt)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# LoadProbe — kết quả đo tính đúng đắn dưới tải đồng thời");
        sb.AppendLine();
        sb.AppendLine($"- **Thời điểm chạy:** {startedAt:yyyy-MM-dd HH:mm:ss} (giờ máy)");
        sb.AppendLine($"- **API:** `{config.ApiBaseUrl}`");
        sb.AppendLine($"- **Giãn cách giữa hai kịch bản:** {config.PaceSeconds}s");
        sb.AppendLine();
        sb.AppendLine("> Cột **Kết luận** đọc như sau: `ĐẠT` = bất biến đúng. `HỎNG` = bất biến sai,");
        sb.AppendLine("> có lỗi đúng đắn dữ liệu. `KHÔNG KẾT LUẬN` = phép đo bị rỗng (bị rate limiter");
        sb.AppendLine("> chặn, seed thiếu, API không phản hồi) — **không được đọc thành \"đạt\"**.");
        sb.AppendLine();

        sb.AppendLine("| # | Kịch bản | Bất biến kiểm sau | Mã HTTP | Kết luận |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var r in reports)
        {
            sb.AppendLine(
                $"| {r.Id} | {r.Title} | {r.Invariant} | {r.Fire?.Histogram() ?? "—"} | {Verdict(r.Outcome.Verdict)} |");
        }
        sb.AppendLine();

        sb.AppendLine("## Chi tiết từng kịch bản");
        sb.AppendLine();
        foreach (var r in reports)
        {
            sb.AppendLine($"### {r.Id} — {r.Title}");
            sb.AppendLine();
            sb.AppendLine($"**Bất biến:** {r.Invariant}");
            sb.AppendLine();
            sb.AppendLine($"**Kết luận:** {Verdict(r.Outcome.Verdict)} — {r.Outcome.Message}");
            sb.AppendLine();
            if (r.Outcome.Details.Count > 0)
            {
                foreach (var d in r.Outcome.Details) sb.AppendLine($"- {d}");
                sb.AppendLine();
            }
            if (r.Fire is { FailureSamples.Count: > 0 })
            {
                sb.AppendLine("Mẫu thân phản hồi thất bại:");
                sb.AppendLine();
                foreach (var sample in r.Fire.FailureSamples)
                {
                    sb.AppendLine("```json");
                    sb.AppendLine(sample);
                    sb.AppendLine("```");
                }
                sb.AppendLine();
            }

            if (r.Error is not null)
            {
                sb.AppendLine("```");
                sb.AppendLine(r.Error);
                sb.AppendLine("```");
                sb.AppendLine();
            }
            sb.AppendLine($"*Thời gian: {r.Elapsed.TotalSeconds:F1}s*");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public static string Verdict(ProbeVerdict v) => v switch
    {
        ProbeVerdict.Pass => "✅ ĐẠT",
        ProbeVerdict.Fail => "🔴 HỎNG",
        ProbeVerdict.Inconclusive => "⚠️ KHÔNG KẾT LUẬN",
        _ => "— bỏ qua"
    };
}
