using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PBL3.Application.Auth;
using PBL3.Shared.DTOs.Auth;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Customers;
using System.Security.Claims;
using PBL3.API.Extensions;

namespace PBL3.API.Controllers.Storefront
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Đăng nhập: Nhận Email + Password, trả về cặp Access Token + Refresh Token.
        /// </summary>
        [HttpPost("login")]
        [EnableRateLimiting("LoginRateLimit")]
        [ProducesResponseType(typeof(ApiResult<TokenResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<TokenResponse>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var result = await _authService.LoginAsync(request);
            return result.ToActionResult(this);
        }

        /// <summary>
        /// Làm mới Token: Nhận cặp Access Token (hết hạn) + Refresh Token, trả về cặp Token mới.
        /// </summary>
        [HttpPost("refresh-token")]
        [ProducesResponseType(typeof(ApiResult<TokenResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<TokenResponse>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            var result = await _authService.RefreshTokenAsync(request);
            return result.ToActionResult(this);
        }

        /// <summary>
        /// Đổi mật khẩu cho người dùng đang đăng nhập.
        /// </summary>
        [HttpPut("change-password")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            {
                return Unauthorized(ApiResult<bool>.Fail("Người dùng chưa đăng nhập."));
            }

            if (request.NewPassword != request.ConfirmPassword)
            {
                return BadRequest(ApiResult<bool>.Fail("Mật khẩu xác nhận không khớp."));
            }

            var result = await _authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
            return result.ToActionResult(this);
        }

        /// <summary>
        /// Đăng ký tài khoản (UC001): Khách hàng tự đăng ký. Trả về thông báo thành công (yêu cầu khách tự đăng nhập).
        /// </summary>
        [HttpPost("register")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] RegisterCustomerRequest request)
        {
            var result = await _authService.RegisterAsync(request);
            return result.ToActionResult(this);
        }
    }
}
