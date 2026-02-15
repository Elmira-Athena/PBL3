using PBL3.Shared.DTOs.Auth;
using PBL3.Shared.DTOs.Common;

namespace PBL3.Service.Auth
{
    public interface IAuthService
    {
        /// <summary>
        /// Xử lý đăng nhập: xác thực email/password, trả về cặp Access + Refresh Token.
        /// </summary>
        Task<ApiResult<TokenResponse>> LoginAsync(LoginRequest request);

        /// <summary>
        /// Cấp lại cặp Token mới khi Access Token hết hạn.
        /// Client gửi lên cặp (Access Token cũ + Refresh Token) để xác thực.
        /// </summary>
        Task<ApiResult<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request);
    }
}
