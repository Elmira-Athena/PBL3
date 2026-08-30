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
        private readonly SessionEndedNotifier _sessionEndedNotifier;
        private readonly TokenRefreshCoordinator _refreshCoordinator;
        private const string BaseUrl = "api/auth";
        private const string TokenKey = "authToken";
        private const string RefreshTokenKey = "refreshToken";

        public AuthClientService(
            HttpClient httpClient,
            ILocalStorageService localStorage,
            AuthenticationStateProvider authStateProvider,
            NavigationManager navigationManager,
            SessionEndedNotifier sessionEndedNotifier,
            TokenRefreshCoordinator refreshCoordinator)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
            _authStateProvider = authStateProvider;
            _navigationManager = navigationManager;
            _sessionEndedNotifier = sessionEndedNotifier;
            _refreshCoordinator = refreshCoordinator;
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

                    // Mở lại cổng chống-phát-trùng của SessionEndedNotifier.
                    // Không reset thì sau MỘT lần hết phiên, mọi lần hết phiên về sau
                    // trong cùng vòng đời tab sẽ bị nuốt và người dùng kẹt ở màn hình
                    // không phản hồi thay vì được đưa về trang đăng nhập.
                    _sessionEndedNotifier.Reset();

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

        public async Task<ApiResult<bool>> ChangePasswordAsync(ChangePasswordRequest request)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"{BaseUrl}/change-password", request);
                var result = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();

                return result ?? ApiResult<bool>.Fail("Đổi mật khẩu thất bại.");
            }
            catch (Exception ex)
            {
                return ApiResult<bool>.Fail($"Lỗi kết nối: {ex.Message}");
            }
        }

        /// <summary>
        /// Làm mới phiên chủ động (gọi sau khi người dùng đổi hồ sơ để claim trong
        /// token khớp lại với dữ liệu mới).
        ///
        /// UỶ QUYỀN cho TokenRefreshCoordinator, KHÔNG tự gọi /refresh-token nữa.
        /// Bản cũ tự gọi, nên nếu nó chạy cùng lúc với nhánh 401 của
        /// AuthHeaderHandler thì có HAI lời gọi refresh song song — mà refresh
        /// token XOAY VÒNG mỗi lần dùng, nên cái thứ hai cầm token đã bị thu hồi:
        /// nó thất bại VÀ vô hiệu hoá luôn kết quả của cái thứ nhất. Người dùng bị
        /// đăng xuất đúng lúc hệ thống đang cố giữ họ đăng nhập.
        /// </summary>
        public async Task RefreshSessionAsync()
        {
            var currentToken = await _refreshCoordinator.GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(currentToken)) return;

            // Truyền token hiện tại làm `staleToken` để coordinator biết đây là một
            // yêu cầu làm mới thật, không phải người đến sau đã có token mới.
            await _refreshCoordinator.TryRefreshAsync(currentToken);
        }
    }
}
