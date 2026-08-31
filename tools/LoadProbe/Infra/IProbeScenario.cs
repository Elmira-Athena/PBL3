namespace PBL3.Tools.LoadProbe.Infra;

/// <summary>
/// Một kịch bản đo. Ba pha tách bạch có chủ đích:
///
///   SetupAsync  — dựng dữ liệu, không đo gì
///   FireAsync   — bắn request đồng thời, chỉ thu thống kê HTTP
///   AssertAsync — mở DbContext MỚI và khẳng định bất biến bằng LINQ
///
/// Giá trị của công cụ nằm ở pha thứ ba. "Bắn 50 request, không có 500 nào" là
/// câu nói gần như vô nghĩa: mọi lỗi đúng đắn dữ liệu ở repo này (voucher vượt
/// hạn mức, log tổn thất nhân đôi, hai phiếu cùng serial) đều trả 200 OK.
/// </summary>
public interface IProbeScenario
{
    /// <summary>Mã kịch bản, dạng "S01".</summary>
    string Id { get; }

    string Title { get; }

    /// <summary>Bất biến sẽ được kiểm — in nguyên văn vào báo cáo.</summary>
    string Invariant { get; }

    /// <summary>Số request kịch bản dự định bắn, dùng để phát hiện phép đo rỗng.</summary>
    int ExpectedRequests { get; }

    Task SetupAsync(ProbeEnvironment env, ProbeFixture fixture);

    Task<FireReport> FireAsync(ProbeEnvironment env);

    Task<ProbeOutcome> AssertAsync(ProbeEnvironment env, FireReport fire);
}
