using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PBL3.Service.Auth;
using PBL3.Shared.DTOs.Auth;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Customers;

namespace PBL3.API.Controllers
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

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
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

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
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

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
    }
}
