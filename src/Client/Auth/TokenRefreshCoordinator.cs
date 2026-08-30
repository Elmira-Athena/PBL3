using System.Net.Http.Json;
using Blazored.LocalStorage;
using PBL3.Shared.DTOs.Auth;
using PBL3.Shared.DTOs.Common;

namespace Client.Auth;

/// <summary>
/// Điều phối việc làm mới access token cho TOÀN BỘ ứng dụng — một cửa duy nhất.
///
/// Ba ràng buộc ép ra thiết kế này, mỗi cái là chỗ một bản viết ngây thơ sẽ vỡ:
///
/// 1. KHÔNG thể dùng HttpClient có gắn AuthHeaderHandler để gọi refresh.
///    Handler bắt 401 rồi gọi lại coordinator, coordinator lại gọi qua handler
///    => đệ quy vô hạn. Vì vậy lớp này dùng named client "HushStoreAPI.Raw",
///    được đăng ký KHÔNG có handler nào (xem Client/Program.cs).
///
/// 2. SINGLE-FLIGHT là bắt buộc, không phải tối ưu. Refresh token XOAY VÒNG mỗi
///    lần dùng: gọi refresh hai lần song song thì lần thứ hai cầm token đã bị
///    thu hồi => nó thất bại VÀ nó vô hiệu hoá luôn kết quả của lần thứ nhất.
///    Người dùng bị đăng xuất đúng vào lúc hệ thống đang cố giữ họ đăng nhập.
///
///    SemaphoreSlim ở client là HỢP LỆ, khác hẳn quy tắc cấm khoá ở backend:
///    một tab WASM là một tiến trình đơn luồng, không có chuyện nhiều instance.
///
/// 3. Sau khi vào được semaphore, PHẢI ĐỌC LẠI token và so với token mà caller
///    đã dùng. 10 request cùng nhận 401 thì chỉ 1 cái thực sự gọi mạng; 9 cái
///    còn lại thức dậy, thấy token đã đổi, và đi thẳng sang bước retry. Bỏ bước
///    so sánh này thì cả 10 cùng gọi refresh nối đuôi nhau và dính đúng bẫy (2).
/// </summary>
public sealed class TokenRefreshCoordinator
{
    /// <summary>Tên named client KHÔNG gắn AuthHeaderHandler. Xem ràng buộc (1).</summary>
    public const string RawClientName = "HushStoreAPI.Raw";

    public const string TokenKey = "authToken";
    public const string RefreshTokenKey = "refreshToken";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILocalStorageService _localStorage;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// Phát sau khi cặp token được ghi mới. JwtAuthenticationStateProvider lắng
    /// nghe sự kiện này.
    ///
    /// Chiều phụ thuộc cố ý là MỘT CHIỀU: coordinator KHÔNG biết gì về
    /// AuthenticationStateProvider. Nếu để nó inject provider thì thành vòng
    /// tròn DI, vì provider cũng cần coordinator để tự làm mới token lúc kiểm
    /// hạn `exp`.
    /// </summary>
    public event Action? TokensChanged;

    public TokenRefreshCoordinator(
        IHttpClientFactory httpClientFactory,
        ILocalStorageService localStorage)
    {
        _httpClientFactory = httpClientFactory;
        _localStorage = localStorage;
    }

    public async Task<string?> GetAccessTokenAsync()
        => Normalize(await _localStorage.GetItemAsStringAsync(TokenKey));

    /// <summary>
    /// Thử làm mới access token.
    /// </summary>
    /// <param name="staleToken">
    /// Token mà caller vừa dùng và bị 401. Dùng để phát hiện "người khác đã làm
    /// mới xong trong lúc mình xếp hàng" — xem ràng buộc (3).
    /// </param>
    /// <returns>Access token mới, hoặc null nếu không làm mới được.</returns>
    public async Task<string?> TryRefreshAsync(string? staleToken, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var current = await GetAccessTokenAsync();

            // Ràng buộc (3): có người đã làm mới xong trong lúc ta xếp hàng.
            // Trả token mới luôn, KHÔNG gọi mạng lần nữa.
            if (!string.IsNullOrWhiteSpace(current) &&
                !string.Equals(current, staleToken, StringComparison.Ordinal))
            {
                return current;
            }

            var refreshToken = Normalize(await _localStorage.GetItemAsStringAsync(RefreshTokenKey));

            // Khách vãng lai (chưa từng đăng nhập) không có gì để làm mới.
            // Đây KHÔNG phải "phiên hết hạn" — phân biệt được chỗ này là thứ sửa
            // lỗi cũ: khách chưa đăng nhập gọi API giỏ hàng bị báo nhầm
            // "Phiên đăng nhập đã hết hạn".
            if (string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(refreshToken))
            {
                return null;
            }

            var raw = _httpClientFactory.CreateClient(RawClientName);

            var response = await raw.PostAsJsonAsync(
                "api/auth/refresh-token",
                new RefreshTokenRequest { AccessToken = current, RefreshToken = refreshToken },
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var result = await response.Content
                .ReadFromJsonAsync<ApiResult<TokenResponse>>(cancellationToken: cancellationToken);

            if (result?.Success != true || result.Data is null ||
                string.IsNullOrWhiteSpace(result.Data.AccessToken))
            {
                return null;
            }

            await _localStorage.SetItemAsStringAsync(TokenKey, result.Data.AccessToken);
            await _localStorage.SetItemAsStringAsync(RefreshTokenKey, result.Data.RefreshToken);

            TokensChanged?.Invoke();

            return result.Data.AccessToken;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Mất mạng, server 500, JSON hỏng — đều quy về "không làm mới được".
            // Caller quyết định kết thúc phiên; ở đây không điều hướng, không ném.
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearTokensAsync()
    {
        await _localStorage.RemoveItemAsync(TokenKey);
        await _localStorage.RemoveItemAsync(RefreshTokenKey);
        TokensChanged?.Invoke();
    }

    /// <summary>
    /// Blazored.LocalStorage đôi khi trả chuỗi còn nguyên cặp ngoặc kép của JSON.
    /// Không cắt thì header thành `Bearer "eyJ..."` và server luôn trả 401.
    /// </summary>
    private static string? Normalize(string? raw)
        => string.IsNullOrWhiteSpace(raw) ? null : raw.Trim('"');
}
