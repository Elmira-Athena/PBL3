using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using PBL3.Shared.DTOs.Auth;
using PBL3.Shared.DTOs.Common;

namespace Client.Auth;

public class AuthHeaderHandler : DelegatingHandler
{
    private readonly ILocalStorageService _localStorage;
    private readonly JwtAuthenticationStateProvider _authStateProvider;
    private readonly NavigationManager _navigationManager;
    private const string TokenKey = "authToken";
    private const string RefreshTokenKey = "refreshToken";

    public AuthHeaderHandler(
        ILocalStorageService localStorage,
        JwtAuthenticationStateProvider authStateProvider,
        NavigationManager navigationManager)
    {
        _localStorage = localStorage;
        _authStateProvider = authStateProvider;
        _navigationManager = navigationManager;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Buffer trước để có thể retry nếu cần
        if (request.Content != null)
            await request.Content.LoadIntoBufferAsync();

        var token = await _localStorage.GetItemAsStringAsync(TokenKey);
        if (!string.IsNullOrWhiteSpace(token))
        {
            token = token.Trim('"');
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);

        // Bỏ qua endpoint auth để tránh redirect loop
        if (response.StatusCode == HttpStatusCode.Unauthorized &&
            !(request.RequestUri?.AbsolutePath.Contains("/api/auth/") ?? false))
        {
            var newToken = await TryRefreshAsync(cancellationToken);
            if (newToken != null)
            {
                // Token mới → retry request gốc, user không bị gián đoạn
                var retry = await CloneRequestAsync(request, newToken);
                return await base.SendAsync(retry, cancellationToken);
            }

            // Refresh thất bại → xóa session, về trang đăng nhập
            await _localStorage.RemoveItemAsync(TokenKey);
            await _localStorage.RemoveItemAsync(RefreshTokenKey);
            _authStateProvider.NotifyAuthStateChanged();
            _navigationManager.NavigateTo("/login?expired=true");
        }

        return response;
    }

    private async Task<string?> TryRefreshAsync(CancellationToken cancellationToken)
    {
        var accessToken = (await _localStorage.GetItemAsStringAsync(TokenKey))?.Trim('"');
        var refreshToken = (await _localStorage.GetItemAsStringAsync(RefreshTokenKey))?.Trim('"');

        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(refreshToken))
            return null;

        try
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh-token");
            req.Content = JsonContent.Create(new RefreshTokenRequest
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken
            });

            var res = await base.SendAsync(req, cancellationToken);
            if (!res.IsSuccessStatusCode) return null;

            var result = await res.Content.ReadFromJsonAsync<ApiResult<TokenResponse>>(
                cancellationToken: cancellationToken);

            if (result?.Success != true || result.Data == null) return null;

            await _localStorage.SetItemAsStringAsync(TokenKey, result.Data.AccessToken);
            await _localStorage.SetItemAsStringAsync(RefreshTokenKey, result.Data.RefreshToken);
            _authStateProvider.NotifyAuthStateChanged();

            return result.Data.AccessToken;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(
        HttpRequestMessage original, string newToken)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);
        clone.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);

        foreach (var header in original.Headers)
        {
            if (header.Key != "Authorization")
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (original.Content != null)
        {
            var bytes = await original.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(bytes);
            foreach (var header in original.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
