namespace Client.Auth;

/// <summary>
/// Lý do phiên làm việc kết thúc, để layout hiển thị đúng thông báo.
/// </summary>
public enum SessionEndedReason
{
    /// <summary>Access token hết hạn và refresh cũng không cứu được.</summary>
    Expired,

    /// <summary>Tài khoản bị quản trị viên khoá (403 + header X-Account-Status: locked).</summary>
    Locked
}

public sealed class SessionEndedEventArgs
{
    public required SessionEndedReason Reason { get; init; }

    /// <summary>Thông báo do server trả về; null thì layout tự chọn câu mặc định.</summary>
    public string? Message { get; init; }
}

/// <summary>
/// Kênh thông báo "phiên đã kết thúc" từ tầng HttpClient lên tầng UI.
///
/// VÌ SAO PHẢI CÓ LỚP NÀY, thay vì gọi thẳng NavigationManager.NavigateTo trong
/// DelegatingHandler như bản cũ:
///
///   NavigateTo huỷ và dựng lại cây component. Nhưng lúc đó ta đang đứng GIỮA
///   một lời gọi SendAsync mà component gọi API vẫn đang await. Component bị
///   dispose trong khi request của chính nó còn đang bay => await tiếp tục chạy
///   trên một component đã chết, sinh ObjectDisposedException hoặc
///   NullReferenceException ở chỗ hoàn toàn không liên quan, rất khó lần ra.
///
///   Tách ra thành sự kiện thì handler chỉ việc "báo", còn quyết định điều hướng
///   thuộc về layout — nơi được phép làm việc đó một cách an toàn.
/// </summary>
public sealed class SessionEndedNotifier
{
    public event Action<SessionEndedEventArgs>? SessionEnded;

    /// <summary>
    /// Chặn phát trùng: 10 request song song cùng nhận 401 thì chỉ nên có MỘT
    /// lần điều hướng về trang đăng nhập, không phải 10.
    /// </summary>
    private bool _alreadyNotified;

    public void Notify(SessionEndedReason reason, string? message = null)
    {
        if (_alreadyNotified) return;
        _alreadyNotified = true;

        SessionEnded?.Invoke(new SessionEndedEventArgs { Reason = reason, Message = message });
    }

    /// <summary>Gọi sau khi đăng nhập lại thành công để mở lại cổng thông báo.</summary>
    public void Reset() => _alreadyNotified = false;
}
