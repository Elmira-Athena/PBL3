namespace PBL3.Tools.LoadProbe.Infra;

public enum ProbeVerdict
{
    /// <summary>Bất biến đúng.</summary>
    Pass,

    /// <summary>Bất biến SAI — có lỗi đúng đắn dữ liệu.</summary>
    Fail,

    /// <summary>
    /// Không kết luận được. Dùng khi phép đo bị RỖNG: request bị rate limiter chặn,
    /// API không lên, dữ liệu seed thiếu. Đây là hạng mục quan trọng nhất của công
    /// cụ này — một kịch bản bị 429 hết mà vẫn in "PASS" là bằng chứng giả.
    /// </summary>
    Inconclusive,

    /// <summary>Bị bỏ qua (không nằm trong --scenarios).</summary>
    Skipped
}

public sealed record ProbeOutcome(
    ProbeVerdict Verdict,
    string Message,
    IReadOnlyList<string> Details)
{
    public static ProbeOutcome Pass(string message, params string[] details)
        => new(ProbeVerdict.Pass, message, details);

    public static ProbeOutcome Fail(string message, params string[] details)
        => new(ProbeVerdict.Fail, message, details);

    public static ProbeOutcome Inconclusive(string message, params string[] details)
        => new(ProbeVerdict.Inconclusive, message, details);
}

/// <summary>Kết quả đầy đủ của một kịch bản, phục vụ bảng markdown.</summary>
public sealed class ScenarioReport
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Invariant { get; init; }
    public ProbeOutcome Outcome { get; set; } =
        new(ProbeVerdict.Skipped, "Không chạy", Array.Empty<string>());
    public FireReport? Fire { get; set; }
    public TimeSpan Elapsed { get; set; }
    public string? Error { get; set; }
}
