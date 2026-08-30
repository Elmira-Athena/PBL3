using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PBL3.Shared.DTOs.Common;

namespace Client.Auth;

/// <summary>
/// Gắn Bearer token vào mọi request, và khi gặp 401 thì LÀM MỚI TOKEN RỒI GỬI LẠI
/// đúng một lần — thay vì đá thẳng người dùng về trang đăng nhập như bản cũ.
///
/// Bản cũ gặp 401 là xoá cả access lẫn refresh token rồi NavigateTo("/login").
/// Nghịch lý: hạ tầng refresh ĐÃ TỒN TẠI VÀ HOẠT ĐỘNG ĐẦY ĐỦ ở cả hai đầu — chỉ
/// là không ai nối nó vào nhánh 401. Vì thế hạ AccessTokenExpirationMinutes
/// xuống 15 khi chưa sửa chỗ này sẽ đăng xuất người dùng mỗi 15 phút và mất
/// trắng dữ liệu form đang nhập dở.
/// </summary>
public class AuthHeaderHandler : DelegatingHandler
{
    private readonly TokenRefreshCoordinator _refreshCoordinator;
    private readonly SessionEndedNotifier _sessionEndedNotifier;

    public AuthHeaderHandler(
        TokenRefreshCoordinator refreshCoordinator,
        SessionEndedNotifier sessionEndedNotifier)
    {
        _refreshCoordinator = refreshCoordinator;
        _sessionEndedNotifier = sessionEndedNotifier;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Đường /api/auth/* (login, register, refresh-token) không bao giờ được
        // đi vào nhánh làm mới token — làm thế là đệ quy.
        var isAuthEndpoint = request.RequestUri?.AbsolutePath
            .Contains("/api/auth/", StringComparison.OrdinalIgnoreCase) ?? false;

        // PHẢI đệm nội dung TRƯỚC lần gửi đầu tiên.
        // HttpRequestMessage KHÔNG dùng lại được sau khi đã gửi, và với body dạng
        // stream thì stream đã bị đọc cạn. Không đệm thì bước gửi lại ném
        // "InvalidOperationException: The request message was already sent".
        if (request.Content is not null)
        {
            await request.Content.LoadIntoBufferAsync();
        }

        var token = await _refreshCoordinator.GetAccessTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);

        // ── Tài khoản bị khoá: 403 + header X-Account-Status: locked ──
        if (response.StatusCode == HttpStatusCode.Forbidden && IsLocked(response))
        {
            // Đọc thông báo bằng BẢN SAO chuỗi, không đụng vào response.Content.
            //
            // Bản cũ gọi ReadFromJsonAsync thẳng trên response rồi vẫn `return response`
            // cho caller đọc lại — nhưng stream đã bị tiêu thụ, caller nhận thân rỗng.
            // Bản cũ còn deserialize SAI KIỂU: backend ghi ApiResult<object>, client
            // đọc ApiResult<TokenResponse>.
            var message = await ReadMessageSafelyAsync(response, cancellationToken);

            await _refreshCoordinator.ClearTokensAsync();
            _sessionEndedNotifier.Notify(SessionEndedReason.Locked, message);

            return response;
        }

        // ── 401: thử làm mới token rồi gửi lại ĐÚNG MỘT LẦN ──
        if (response.StatusCode != HttpStatusCode.Unauthorized ||
            isAuthEndpoint ||
            cancellationToken.IsCancellationRequested)
        {
            return response;
        }

        // Khách vãng lai chưa từng đăng nhập mà gọi endpoint cần quyền: đây là
        // "chưa đăng nhập", KHÔNG phải "phiên hết hạn". Trả nguyên 401 cho caller
        // tự xử lý, không phát thông báo hết hạn gây hiểu nhầm.
        if (string.IsNullOrWhiteSpace(token))
        {
            return response;
        }

        var newToken = await _refreshCoordinator.TryRefreshAsync(token, cancellationToken);

        if (string.IsNullOrWhiteSpace(newToken))
        {
            // Refresh token cũng hết hạn hoặc đã bị thu hồi — hết đường cứu.
            await _refreshCoordinator.ClearTokensAsync();
            _sessionEndedNotifier.Notify(SessionEndedReason.Expired);
            return response;
        }

        // Gửi lại. AN TOÀN KỂ CẢ VỚI POST, và chỉ an toàn ở ĐÚNG chỗ này:
        // 401 do middleware JWT phát ra TRƯỚC KHI controller chạy, nên server
        // chưa làm bất cứ việc gì. Không có tác dụng phụ nào để nhân đôi.
        //
        // ⚠️ ĐỪNG mở rộng cách này sang 500 hay 409 — ở đó controller ĐÃ chạy,
        // và gửi lại là nhân đôi nghiệp vụ (đơn hàng, phiếu, khoản thanh toán).
        response.Dispose();

        var retryRequest = await CloneRequestAsync(request);
        retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);

        return await base.SendAsync(retryRequest, cancellationToken);
    }

    private static bool IsLocked(HttpResponseMessage response)
        => response.Headers.TryGetValues("X-Account-Status", out var values) &&
           values.FirstOrDefault() == "locked";

    /// <summary>
    /// Đọc trường Message mà KHÔNG làm hỏng response cho caller: đọc ra chuỗi,
    /// rồi gán lại một StringContent mới giữ nguyên header.
    /// </summary>
    private static async Task<string?> ReadMessageSafelyAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            var replacement = new StringContent(body);
            foreach (var header in response.Content.Headers)
            {
                replacement.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            response.Content = replacement;

            return System.Text.Json.JsonSerializer
                .Deserialize<ApiResult<object>>(body, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                })?.Message;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Dựng một HttpRequestMessage mới sao chép toàn bộ từ bản cũ.
    /// Bắt buộc vì một HttpRequestMessage đã gửi thì không gửi lại được.
    /// </summary>
    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version
        };

        if (request.Content is not null)
        {
            // Content đã được LoadIntoBufferAsync ở đầu SendAsync nên đọc lại được.
            var buffer = new MemoryStream();
            await request.Content.CopyToAsync(buffer);
            buffer.Position = 0;

            var content = new StreamContent(buffer);
            foreach (var header in request.Content.Headers)
            {
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            clone.Content = content;
        }

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // Options mang cả cấu hình fetch của trình duyệt trong Blazor WASM
        // (ví dụ BrowserResponseStreamingEnabled). Bỏ qua là đổi hành vi ngầm.
        foreach (var option in request.Options)
        {
            ((IDictionary<string, object?>)clone.Options)[option.Key] = option.Value;
        }

        return clone;
    }
}
