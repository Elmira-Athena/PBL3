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
        var token = await _localStorage.GetItemAsStringAsync(TokenKey);
        if (!string.IsNullOrWhiteSpace(token))
        {
            token = token.Trim('"');
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

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

        // Token hết hạn hoặc không hợp lệ → về trang đăng nhập
        if (response.StatusCode == HttpStatusCode.Unauthorized &&
            !(request.RequestUri?.AbsolutePath.Contains("/api/auth/") ?? false) &&
            !cancellationToken.IsCancellationRequested)
        {
            await _localStorage.RemoveItemAsync(TokenKey);
            await _localStorage.RemoveItemAsync(RefreshTokenKey);
            _authStateProvider.NotifyAuthStateChanged();
            _navigationManager.NavigateTo("/login?expired=true");
        }

        return response;
    }
}
