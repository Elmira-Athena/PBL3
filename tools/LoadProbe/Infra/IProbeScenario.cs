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

    /// <summary>
    /// <c>true</c> nghĩa là CHÍNH rate limiter là thứ đang được đo, nên 429 là kết quả MONG
    /// ĐỢI chứ không phải dấu hiệu phép đo rỗng.
    ///
    /// 🚨 Cờ này chỉ tắt DUY NHẤT nhánh 429 của <see cref="FireReport.VacuityReason"/>. Các
    /// cửa chặn khác (lỗi tầng vận chuyển, bắn thiếu request) vẫn giữ nguyên — nếu không thì
    /// một kịch bản bật cờ này sẽ mất luôn lưới an toàn và "0 request nào tới được API" cũng
    /// đọc thành ĐẠT.
    ///
    /// Mặc định <c>false</c> ở <c>ScenarioBase</c>: tám kịch bản còn lại đo tính đúng đắn dữ
    /// liệu, và với chúng 429 vẫn là bằng chứng rỗng như trước.
    /// </summary>
    bool RateLimitIsUnderTest { get; }

    Task SetupAsync(ProbeEnvironment env, ProbeFixture fixture);

    Task<FireReport> FireAsync(ProbeEnvironment env);

    Task<ProbeOutcome> AssertAsync(ProbeEnvironment env, FireReport fire);
}
