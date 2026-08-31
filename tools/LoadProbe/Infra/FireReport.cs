using System.Collections.Concurrent;
using System.Net;

namespace PBL3.Tools.LoadProbe.Infra;

/// <summary>Thống kê một loạt request bắn song song.</summary>
public sealed class FireReport
{
    private readonly ConcurrentDictionary<int, int> _statusCounts = new();
    private readonly ConcurrentBag<string> _transportErrors = new();
    private readonly ConcurrentBag<string> _failureSamples = new();

    public int Total => _statusCounts.Values.Sum() + _transportErrors.Count;
    public int RateLimited => _statusCounts.TryGetValue(429, out var n) ? n : 0;
    public int ServerErrors => _statusCounts.Where(kv => kv.Key >= 500).Sum(kv => kv.Value);
    public int Successes => _statusCounts.Where(kv => kv.Key is >= 200 and < 300).Sum(kv => kv.Value);
    public IReadOnlyCollection<string> TransportErrors => _transportErrors;

    public void Record(HttpStatusCode status)
        => _statusCounts.AddOrUpdate((int)status, 1, (_, n) => n + 1);

    public void RecordTransportError(string message)
        => _transportErrors.Add(message);

    /// <summary>
    /// Giữ vài thân phản hồi thất bại đầu tiên.
    ///
    /// Không phải để trang trí: "33 lần 400" tự nó không nói được gì. Thông báo lỗi
    /// của API đều bằng tiếng Việt và rất cụ thể ("hết hàng", "địa chỉ không hợp lệ"),
    /// nên một mẫu thân phản hồi tiết kiệm cả một vòng chẩn đoán bằng tay.
    /// </summary>
    public void RecordFailureSample(string body)
    {
        if (_failureSamples.Count >= 3) return;
        var trimmed = body.Length > 300 ? body[..300] : body;
        if (!_failureSamples.Contains(trimmed)) _failureSamples.Add(trimmed);
    }

    public IReadOnlyCollection<string> FailureSamples => _failureSamples;

    /// <summary>Gộp thống kê của một loạt bắn khác vào loạt này.</summary>
    public void MergeFrom(FireReport other)
    {
        foreach (var kv in other._statusCounts)
        {
            _statusCounts.AddOrUpdate(kv.Key, kv.Value, (_, n) => n + kv.Value);
        }
        foreach (var err in other._transportErrors)
        {
            _transportErrors.Add(err);
        }
    }

    /// <summary>Ví dụ: "200×1, 400×49".</summary>
    public string Histogram()
    {
        var parts = _statusCounts
            .OrderBy(kv => kv.Key)
            .Select(kv => $"{kv.Key}×{kv.Value}")
            .ToList();
        if (!_transportErrors.IsEmpty) parts.Add($"lỗi-mạng×{_transportErrors.Count}");
        return parts.Count == 0 ? "(không có request nào)" : string.Join(", ", parts);
    }

    /// <summary>
    /// Lý do khiến phép đo trở nên RỖNG, hoặc null nếu phép đo hợp lệ.
    ///
    /// Gọi hàm này TRƯỚC khi kết luận Pass. Một kịch bản mà 49/50 request ăn 429
    /// vẫn thoả mọi bất biến — vì code cần kiểm chưa từng chạy.
    /// </summary>
    public string? VacuityReason(int expectedRequests)
    {
        if (RateLimited > 0)
        {
            return $"{RateLimited}/{Total} request bị rate limiter chặn (429). Tăng --pace " +
                   "hoặc giảm số request; trần chung là 100 request/10 giây mỗi IP.";
        }

        if (!_transportErrors.IsEmpty)
        {
            return $"{_transportErrors.Count} request lỗi tầng vận chuyển: " +
                   string.Join(" | ", _transportErrors.Take(3));
        }

        if (Total < expectedRequests)
        {
            return $"Chỉ ghi nhận {Total}/{expectedRequests} request.";
        }

        return null;
    }
}
