using PBL3.Shared.DTOs.Auth;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Customers;

namespace PBL3.Application.Auth
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

        /// <summary>
        /// UC001: Khách hàng tự đăng ký tài khoản.
        /// Tạo tài khoản mới, yêu cầu đăng nhập lại (không trả JWT).
        /// </summary>
        Task<ApiResult<bool>> RegisterAsync(RegisterCustomerRequest request);

        /// <summary>
        /// Đổi mật khẩu cho người dùng hiện tại.
        /// </summary>
        Task<ApiResult<bool>> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
    }
}
