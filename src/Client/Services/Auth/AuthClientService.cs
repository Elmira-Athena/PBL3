using System.Net.Http.Json;
using Blazored.LocalStorage;
using Client.Auth;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using PBL3.Shared.DTOs.Auth;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Customers;

namespace Client.Services.Auth
{
    public class AuthClientService : IAuthClientService
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalStorageService _localStorage;
        private readonly AuthenticationStateProvider _authStateProvider;
        private readonly NavigationManager _navigationManager;
        private const string BaseUrl = "api/auth";
        private const string TokenKey = "authToken";
        private const string RefreshTokenKey = "refreshToken";

        public AuthClientService(
            HttpClient httpClient,
            ILocalStorageService localStorage,
            AuthenticationStateProvider authStateProvider,
            NavigationManager navigationManager)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
            _authStateProvider = authStateProvider;
            _navigationManager = navigationManager;
        }

        public async Task<ApiResult<TokenResponse>> LoginAsync(LoginRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/login", request);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<TokenResponse>>();

                if (result != null && result.Success && result.Data != null)
                {
                    // Lưu token vào LocalStorage
                    await _localStorage.SetItemAsStringAsync(TokenKey, result.Data.AccessToken);
                    await _localStorage.SetItemAsStringAsync(RefreshTokenKey, result.Data.RefreshToken);

                    // Báo cho AuthStateProvider biết đã đăng nhập
                    ((JwtAuthenticationStateProvider)_authStateProvider).NotifyAuthStateChanged();
                }

                return result ?? ApiResult<TokenResponse>.Fail("Đăng nhập thất bại.");
            }
            catch (Exception ex)
            {
                return ApiResult<TokenResponse>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        public async Task LogoutAsync()
        {
            // Xóa token khỏi LocalStorage
            await _localStorage.RemoveItemAsync(TokenKey);
            await _localStorage.RemoveItemAsync(RefreshTokenKey);

            // Báo cho AuthStateProvider biết đã đăng xuất
            ((JwtAuthenticationStateProvider)_authStateProvider).NotifyAuthStateChanged();

            // Redirect về trang đăng nhập
            _navigationManager.NavigateTo("/login");
        }

        public async Task<ApiResult<bool>> RegisterAsync(RegisterCustomerRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/register", request);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();

                return result ?? ApiResult<bool>.Fail("Đăng ký thất bại.");
            }
            catch (Exception ex)
            {
                return ApiResult<bool>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }
    }
}
