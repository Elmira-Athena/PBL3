using System.Net.Http.Headers;
using Blazored.LocalStorage;

namespace Client.Auth;

/// <summary>
/// DelegatingHandler tự động gắn JWT vào mọi HTTP request gọi xuống API.
/// Hoạt động như một Interceptor: mỗi khi HttpClient gọi GetAsync/PostAsync/...,
/// handler này sẽ chặn lại, lấy token từ LocalStorage và nhét vào header
/// "Authorization: Bearer {token}" trước khi cho request đi tiếp.
/// </summary>
public class AuthHeaderHandler : DelegatingHandler
{
    private readonly ILocalStorageService _localStorage;
    private const string TokenKey = "authToken";

    public AuthHeaderHandler(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Lấy token từ LocalStorage
        var token = await _localStorage.GetItemAsStringAsync(TokenKey);

        if (!string.IsNullOrWhiteSpace(token))
        {
            // Loại bỏ dấu ngoặc kép nếu có (LocalStorage có thể bọc quotes)
            token = token.Trim('"');

            // Gắn Bearer token vào Authorization header
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        // Cho request đi tiếp tới inner handler (và cuối cùng ra network)
        return await base.SendAsync(request, cancellationToken);
    }
}
