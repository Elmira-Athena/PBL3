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
    private static readonly SemaphoreSlim _refreshSemaphore = new(1, 1);

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
        var sentToken = token?.Trim('"');

        var response = await base.SendAsync(request, cancellationToken);

        // Tài khoản bị khóa → 403 với header X-Account-Status: locked
        if (response.StatusCode == HttpStatusCode.Forbidden &&
            response.Headers.TryGetValues("X-Account-Status", out var statusValues) &&
            statusValues.FirstOrDefault() == "locked")
        {
            var result = await response.Content.ReadFromJsonAsync<ApiResult<TokenResponse>>(
                cancellationToken: cancellationToken);
            await _localStorage.RemoveItemAsync(TokenKey);
            await _localStorage.RemoveItemAsync(RefreshTokenKey);
            _authStateProvider.NotifyAuthStateChanged();
            var encoded = Uri.EscapeDataString(result?.Message ?? "Tài khoản của bạn đã bị khóa.");
            _navigationManager.NavigateTo($"/login?locked=true&reason={encoded}");
            return response;
        }

        // Bỏ qua endpoint auth để tránh redirect loop
        if (response.StatusCode == HttpStatusCode.Unauthorized &&
            !(request.RequestUri?.AbsolutePath.Contains("/api/auth/") ?? false))
        {
            var (newToken, refreshError) = await TryRefreshAsync(sentToken, cancellationToken);
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

            if (refreshError?.Contains("bị khóa") == true)
            {
                var encoded = Uri.EscapeDataString(refreshError);
                _navigationManager.NavigateTo($"/login?locked=true&reason={encoded}");
            }
            else
            {
                _navigationManager.NavigateTo("/login?expired=true");
            }
        }

        return response;
    }

    private async Task<(string? Token, string? ErrorMessage)> TryRefreshAsync(
        string? sentToken, CancellationToken cancellationToken)
    {
        await _refreshSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Nếu một concurrent call đã refresh trước → dùng token mới ngay, không gọi API
            var currentToken = (await _localStorage.GetItemAsStringAsync(TokenKey))?.Trim('"');
            if (currentToken != null && currentToken != sentToken)
                return (currentToken, null);

            var accessToken = currentToken;
            var refreshToken = (await _localStorage.GetItemAsStringAsync(RefreshTokenKey))?.Trim('"');

            if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(refreshToken))
                return (null, null);

            try
            {
                var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh-token");
                req.Content = JsonContent.Create(new RefreshTokenRequest
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken
                });

                var res = await base.SendAsync(req, cancellationToken);
                if (!res.IsSuccessStatusCode)
                {
                    string? errorMessage = null;
                    try
                    {
                        var errResult = await res.Content.ReadFromJsonAsync<ApiResult<TokenResponse>>(
                            cancellationToken: cancellationToken);
                        errorMessage = errResult?.Message;
                    }
                    catch { }
                    return (null, errorMessage);
                }

                var result = await res.Content.ReadFromJsonAsync<ApiResult<TokenResponse>>(
                    cancellationToken: cancellationToken);

                if (result?.Success != true || result.Data == null) return (null, result?.Message);

                await _localStorage.SetItemAsStringAsync(TokenKey, result.Data.AccessToken);
                await _localStorage.SetItemAsStringAsync(RefreshTokenKey, result.Data.RefreshToken);
                _authStateProvider.NotifyAuthStateChanged();

                return (result.Data.AccessToken, null);
            }
            catch
            {
                return (null, null);
            }
        }
        finally
        {
            _refreshSemaphore.Release();
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
