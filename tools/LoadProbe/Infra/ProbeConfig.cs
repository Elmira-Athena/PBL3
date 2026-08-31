namespace PBL3.Tools.LoadProbe.Infra;

/// <summary>
/// Cấu hình một lần chạy đo. Đọc từ biến môi trường (cùng TÊN với API, để cùng
/// một shell chạy được cả API lẫn probe) và từ tham số dòng lệnh.
/// </summary>
public sealed class ProbeConfig
{
    public required string ApiBaseUrl { get; init; }
    public required string ConnectionString { get; init; }
    public required string JwtSecret { get; init; }
    public required string JwtIssuer { get; init; }
    public required string JwtAudience { get; init; }
    public required string OutputDirectory { get; init; }
    public required IReadOnlyList<string> ScenarioIds { get; init; }

    /// <summary>
    /// Giãn cách giữa hai kịch bản, tính bằng giây.
    ///
    /// KHÔNG PHẢI thứ trang trí. Trần chung của API là 100 request / 10 giây cho
    /// MỖI IP (Program.cs, khối GlobalLimiter). Probe bắn từ đúng một IP, nên hai
    /// kịch bản chạy sát nhau sẽ dùng chung một cửa sổ và kịch bản sau ăn 429.
    /// Mặc định 11 giây để cửa sổ 10 giây chắc chắn đã lăn qua.
    /// </summary>
    public int PaceSeconds { get; init; } = 11;

    /// <summary>Giữ lại dữ liệu đã seed để soi bằng tay thay vì dọn.</summary>
    public bool KeepData { get; init; }

    public static ProbeConfig Parse(string[] args)
    {
        string Arg(string name, string? fallback = null)
        {
            var i = Array.IndexOf(args, "--" + name);
            if (i >= 0 && i + 1 < args.Length) return args[i + 1];
            return fallback ?? string.Empty;
        }

        bool Flag(string name) => Array.IndexOf(args, "--" + name) >= 0;

        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Thiếu biến môi trường ConnectionStrings__DefaultConnection. " +
                "Xem docs/bat-dau-phien-moi.md mục 3.");
        }

        var jwtSecret = Environment.GetEnvironmentVariable("JwtSettings__SecretKey") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(jwtSecret))
        {
            throw new InvalidOperationException(
                "Thiếu biến môi trường JwtSettings__SecretKey. Probe tự ký token nên PHẢI " +
                "dùng đúng khoá mà API đang chạy dùng, nếu không mọi request đều 401.");
        }

        var pace = Arg("pace");

        return new ProbeConfig
        {
            ApiBaseUrl = Arg("api", "http://localhost:5222").TrimEnd('/'),
            ConnectionString = connectionString,
            JwtSecret = jwtSecret,
            JwtIssuer = Environment.GetEnvironmentVariable("JwtSettings__Issuer") ?? "HushStoreAPI",
            JwtAudience = Environment.GetEnvironmentVariable("JwtSettings__Audience")
                          ?? "HushStoreBlazorClient",
            OutputDirectory = Arg("out", "docs/evidence/loadprobe"),
            ScenarioIds = ParseScenarios(Arg("scenarios", "all")),
            PaceSeconds = int.TryParse(pace, out var p) ? p : 11,
            KeepData = Flag("keep")
        };
    }

    private static IReadOnlyList<string> ParseScenarios(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<string>(); // rỗng = chạy tất cả
        }

        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  .Select(s => s.StartsWith("S", StringComparison.OrdinalIgnoreCase) ? s.ToUpperInvariant() : "S" + s.PadLeft(2, '0'))
                  .ToList();
    }
}
