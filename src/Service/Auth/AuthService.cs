using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PBL3.Core.Entities;
using PBL3.Infrastructure.Data;
using PBL3.Shared.DTOs.Auth;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Customers;

namespace PBL3.Service.Auth
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly HushStoreDbContext _context;

        public AuthService(UserManager<AppUser> userManager, IConfiguration configuration, HushStoreDbContext context)
        {
            _userManager = userManager;
            _configuration = configuration;
            _context = context;
        }

        // =====================================================================
        // LOGIN
        // =====================================================================
        public async Task<ApiResult<TokenResponse>> LoginAsync(LoginRequest request)
        {
            // 1. Tìm User theo Email
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                return ApiResult<TokenResponse>.Fail("Tài khoản hoặc mật khẩu không đúng.");
            }

            // 2. Kiểm tra tài khoản bị khóa (Lockout do brute-force)
            if (await _userManager.IsLockedOutAsync(user))
            {
                return ApiResult<TokenResponse>.Fail(
                    "Tài khoản đã bị tạm khóa do đăng nhập sai quá nhiều lần. Vui lòng thử lại sau.");
            }

            // 3. Kiểm tra mật khẩu
            var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
            if (!passwordValid)
            {
                // Tăng số lần đăng nhập sai (Lockout counter)
                await _userManager.AccessFailedAsync(user);
                return ApiResult<TokenResponse>.Fail("Tài khoản hoặc mật khẩu không đúng.");
            }

            // 4. Reset lockout counter khi đăng nhập đúng
            await _userManager.ResetAccessFailedCountAsync(user);

            // 5. Kiểm tra IsActive (Tài khoản bị vô hiệu hóa bởi Admin)
            if (!user.IsActive)
            {
                return ApiResult<TokenResponse>.Fail("Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên.");
            }

            // 6. Sinh Access Token (JWT) với đầy đủ Claims
            var accessToken = await GenerateJwtTokenAsync(user);

            // 7. Sinh Refresh Token ngẫu nhiên
            var refreshToken = GenerateRefreshToken();

            // 8. Lưu Refresh Token vào DB
            var refreshTokenExpirationDays = _configuration.GetValue<int>("JwtSettings:RefreshTokenExpirationDays");
            user.RefreshToken = HashToken(refreshToken);
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(refreshTokenExpirationDays);
            await _userManager.UpdateAsync(user);

            // 9. Trả về cặp Token
            return ApiResult<TokenResponse>.Ok(new TokenResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken
            }, "Đăng nhập thành công.");
        }

        // =====================================================================
        // REFRESH TOKEN
        // =====================================================================
        public async Task<ApiResult<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request)
        {
            // 1. Bóc tách Access Token (dù đã hết hạn) để lấy UserId
            var principal = GetPrincipalFromExpiredToken(request.AccessToken);
            if (principal == null)
            {
                return ApiResult<TokenResponse>.Fail("Access Token không hợp lệ.");
            }

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return ApiResult<TokenResponse>.Fail("Không thể xác định người dùng từ Token.");
            }

            // 2. Tìm User trong DB
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return ApiResult<TokenResponse>.Fail("Người dùng không tồn tại.");
            }

            // 3. Kiểm tra Refresh Token có khớp và còn hạn không
            var hashedToken = HashToken(request.RefreshToken);
            if (user.RefreshToken != hashedToken)
            {
                return ApiResult<TokenResponse>.Fail("Refresh Token không hợp lệ.");
            }

            if (user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            {
                return ApiResult<TokenResponse>.Fail("Refresh Token đã hết hạn. Vui lòng đăng nhập lại.");
            }

            // 4. Kiểm tra tài khoản còn hoạt động không
            if (!user.IsActive)
            {
                // Thu hồi Refresh Token khi tài khoản bị khóa
                user.RefreshToken = null;
                user.RefreshTokenExpiryTime = null;
                await _userManager.UpdateAsync(user);
                return ApiResult<TokenResponse>.Fail("Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên.");
            }

            // 5. Sinh cặp Token MỚI (Token Rotation)
            var newAccessToken = await GenerateJwtTokenAsync(user);
            var newRefreshToken = GenerateRefreshToken();

            // 6. Lưu Refresh Token mới vào DB (Invalidate cái cũ)
            var refreshTokenExpirationDays = _configuration.GetValue<int>("JwtSettings:RefreshTokenExpirationDays");
            user.RefreshToken = HashToken(newRefreshToken);
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(refreshTokenExpirationDays);
            await _userManager.UpdateAsync(user);

            return ApiResult<TokenResponse>.Ok(new TokenResponse
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken
            }, "Làm mới Token thành công.");
        }

        // =====================================================================
        // PRIVATE: Sinh JWT Access Token
        // =====================================================================
        private async Task<string> GenerateJwtTokenAsync(AppUser user)
        {
            // Đọc cấu hình từ appsettings.json — KHÔNG BAO GIỜ HARDCODE
            var secretKey = _configuration["JwtSettings:SecretKey"]
                ?? throw new InvalidOperationException("JwtSettings:SecretKey chưa được cấu hình.");
            var issuer = _configuration["JwtSettings:Issuer"];
            var audience = _configuration["JwtSettings:Audience"];
            var expirationMinutes = _configuration.GetValue<int>("JwtSettings:AccessTokenExpirationMinutes");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Claims chuẩn: NameIdentifier (UserId), Email
            // Load FullName từ UserProfile
            var profile = await _context.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == user.Id);
            var fullName = profile?.FullName ?? string.Empty;

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Email, user.Email ?? string.Empty),
                new(ClaimTypes.Name, fullName),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            // Duyệt vòng lặp để add TOÀN BỘ Roles của User
            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            // ⚠️ TUYỆT ĐỐI KHÔNG nhét Password hay thông tin nhạy cảm vào Claims!

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // =====================================================================
        // PRIVATE: Sinh chuỗi Refresh Token ngẫu nhiên (cryptographically secure)
        // =====================================================================
        private static string GenerateRefreshToken()
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }

        // =====================================================================
        // PRIVATE: Hash Token bằng SHA256
        // =====================================================================
        private static string HashToken(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToBase64String(bytes);
        }

        // =====================================================================
        // PRIVATE: Bóc tách Access Token đã hết hạn để lấy Claims
        // =====================================================================
        private ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            var secretKey = _configuration["JwtSettings:SecretKey"]
                ?? throw new InvalidOperationException("JwtSettings:SecretKey chưa được cấu hình.");

            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = true,
                ValidAudience = _configuration["JwtSettings:Audience"],
                ValidateIssuer = true,
                ValidIssuer = _configuration["JwtSettings:Issuer"],
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ValidateLifetime = false // ← Quan trọng: Chấp nhận token đã hết hạn
            };

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);

                // Kiểm tra thuật toán ký có đúng HmacSha256 không (chống token giả)
                if (securityToken is not JwtSecurityToken jwtToken ||
                    !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256,
                        StringComparison.InvariantCultureIgnoreCase))
                {
                    return null;
                }

                return principal;
            }
            catch
            {
                return null; // Token không hợp lệ hoặc bị tamper
            }
        }

        // =====================================================================
        // REGISTER (UC001: Khách hàng tự đăng ký)
        // =====================================================================
        public async Task<ApiResult<bool>> RegisterAsync(RegisterCustomerRequest request)
        {
            // 1. Kiểm tra Email đã tồn tại chưa
            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null)
            {
                return ApiResult<bool>.Fail("Email này đã được sử dụng.");
            }

            // 2. Kiểm tra SĐT đã tồn tại chưa
            var existingPhone = _context.Users.Any(u => u.PhoneNumber == request.PhoneNumber);
            if (existingPhone)
            {
                return ApiResult<bool>.Fail("Số điện thoại này đã được sử dụng.");
            }

            // 3. Tạo AppUser
            var user = new AppUser
            {
                Id = Guid.NewGuid(),
                UserName = request.Email,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                IsActive = true,
                Type = 2, // Customer
                CreatedDate = DateTime.UtcNow,
                IsDeleted = false
            };

            var createResult = await _userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                return ApiResult<bool>.Fail("Đăng ký thất bại: " + errors);
            }

            // 4. Tạo UserProfile
            _context.UserProfiles.Add(new UserProfile
            {
                UserId = user.Id,
                FullName = request.FullName.Trim()
            });
            await _context.SaveChangesAsync();

            // 5. Assign role
            await _userManager.AddToRoleAsync(user, "Customer");

            return ApiResult<bool>.Ok(true, "Đăng ký tài khoản thành công. Vui lòng đăng nhập.");
        }
    }
}
