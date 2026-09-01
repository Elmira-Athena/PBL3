using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            UserManager<AppUser> userManager,
            IConfiguration configuration,
            HushStoreDbContext context,
            ILogger<AuthService> logger)
        {
            _userManager = userManager;
            _configuration = configuration;
            _context = context;
            _logger = logger;
        }

        // =====================================================================
        // LOGIN
        // =====================================================================
        /// <summary>
        /// NGHIỆP VỤ: Đăng nhập bảo mật vào hệ thống.
        /// Quản lý luồng kiểm tra thông tin đăng nhập, bảo vệ chống tấn công Brute-force và cấp phát cặp Token (Access Token &amp; Refresh Token).
        /// Quy trình nghiệp vụ:
        /// 1. Tìm kiếm người dùng theo Email đã đăng ký.
        /// 2. Xác thực trạng thái kích hoạt (Active) để đảm bảo tài khoản không bị vô hiệu hóa hoặc đình chỉ bởi quản trị viên.
        /// 3. Xác thực phòng ngừa Brute-force (kiểm tra tài khoản có đang trong trạng thái khóa tạm thời Lockout do đăng nhập sai quá nhiều lần liên tục hay không).
        /// 4. Kiểm tra tính hợp lệ của mật khẩu. Nếu không hợp lệ, kích hoạt tăng số lần đăng nhập sai (AccessFailedAsync) nhằm tự động khóa tài khoản khi vượt ngưỡng quy định.
        /// 5. Nếu mật khẩu hợp lệ, reset bộ đếm thất bại về 0 để mở khóa hoàn toàn tài khoản.
        /// 6. Sinh Access Token dạng JWT chứa đầy đủ thông tin định danh cơ bản cùng danh sách toàn bộ các vai trò (Roles) và quyền hạn (Claims) của người dùng.
        /// 7. Sinh Refresh Token ngẫu nhiên có độ an toàn mã hóa cực cao (Cryptographically Secure Pseudo-Random).
        /// 8. Thực hiện băm một chiều Refresh Token bằng thuật toán SHA256 trước khi lưu trữ trong cơ sở dữ liệu để bảo vệ chống rò rỉ, thiết lập thời hạn hết hạn.
        /// 9. Trả về cặp Token thành công.
        /// </summary>
        public async Task<ApiResult<TokenResponse>> LoginAsync(LoginRequest request)
        {
            // 1. Tìm User theo Email
            // LƯU Ý NGHIỆP VỤ: Email đóng vai trò định danh duy nhất trong hệ thống cho tài khoản AppUser
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                return ApiResult<TokenResponse>.Fail("Tài khoản hoặc mật khẩu không đúng.");
            }

            // 2. Kiểm tra IsActive (Tài khoản bị vô hiệu hóa bởi Admin) — thực hiện trước khi check password để tối ưu hiệu năng
            if (!user.IsActive)
            {
                var reason = !string.IsNullOrEmpty(user.LockReason) ? user.LockReason : "Vui lòng liên hệ quản trị viên.";
                return ApiResult<TokenResponse>.Fail($"Tài khoản đã bị khóa. Lý do: {reason}");
            }

            // 3. Kiểm tra tài khoản bị khóa (Lockout do brute-force)
            // LƯU Ý BẢO MẬT: Ngăn chặn tự động dò quét mật khẩu từ các công cụ tấn công bên ngoài bằng cách tạm khóa IP/tài khoản
            if (await _userManager.IsLockedOutAsync(user))
            {
                return ApiResult<TokenResponse>.Fail(
                    "Tài khoản đã bị tạm khóa do đăng nhập sai quá nhiều lần. Vui lòng thử lại sau.");
            }

            // 4. Kiểm tra mật khẩu
            var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
            if (!passwordValid)
            {
                // Tăng số lần đăng nhập sai (Lockout counter) trong cấu hình của Identity framework
                await _userManager.AccessFailedAsync(user);
                return ApiResult<TokenResponse>.Fail("Tài khoản hoặc mật khẩu không đúng.");
            }

            // 5. Reset lockout counter khi đăng nhập đúng
            // Trả lại tài khoản về trạng thái an toàn tuyệt đối
            await _userManager.ResetAccessFailedCountAsync(user);

            // 6. Sinh Access Token (JWT) với đầy đủ Claims để duy trì phiên làm việc phi trạng thái (stateless)
            var accessToken = await GenerateJwtTokenAsync(user);

            // 7. Sinh Refresh Token ngẫu nhiên phục vụ cơ chế xoay vòng token khi access token hết hạn
            var refreshToken = GenerateRefreshToken();

            // 8. Lưu Refresh Token vào DB
            // LƯU Ý BẢO MẬT: Chỉ lưu trữ mã băm SHA256 một chiều của Refresh Token thay vì lưu text trần để phòng ngừa rò rỉ dữ liệu DB
            var refreshTokenExpirationDays = _configuration.GetValue<int>("JwtSettings:RefreshTokenExpirationDays");
            user.RefreshToken = HashToken(refreshToken);
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(refreshTokenExpirationDays);
            await _userManager.UpdateAsync(user);

            // 9. Trả về cặp Token cho thiết bị Client
            return ApiResult<TokenResponse>.Ok(new TokenResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken
            }, "Đăng nhập thành công.");
        }

        // =====================================================================
        // REFRESH TOKEN
        // =====================================================================
        /// <summary>
        /// NGHIỆP VỤ: Làm mới mã truy cập (Cơ chế Token Rotation bảo mật).
        /// Cho phép người dùng gia hạn phiên làm việc khi Access Token hết hiệu lực bằng cách sử dụng một Refresh Token hợp lệ còn hạn.
        /// Quy trình nghiệp vụ:
        /// 1. Bóc tách thông tin Claims từ Access Token đã hết hạn (chấp nhận hết hạn thời gian) để định danh người dùng sở hữu token đó (UserId).
        /// 2. Truy vấn thực thể AppUser từ Database để kiểm tra sự tồn tại.
        /// 3. Xác thực trạng thái kích hoạt (Active) của người dùng để kịp thời ngắt quyền truy cập đối với các tài khoản bị khóa trong thời gian thực.
        /// 4. Thực hiện mã hóa SHA256 Refresh Token do Client gửi lên và so sánh trực tiếp với mã băm lưu trong DB nhằm tránh giả mạo.
        /// 5. Xác thực thời gian hết hạn của Refresh Token. Nếu hết hạn, cưỡng chế phiên làm việc phải đăng nhập lại hoàn toàn.
        /// 6. Kích hoạt cơ chế bảo mật Xoay vòng Token (Token Rotation):
        ///    - Khởi tạo Access Token mới.
        ///    - Khởi tạo Refresh Token ngẫu nhiên hoàn toàn mới.
        /// 7. Vô hiệu hóa triệt để Refresh Token cũ bằng cách ghi đè mã băm của Refresh Token mới vào DB (giúp phòng ngừa đòn tấn công replay mã token bị đánh cắp).
        /// 8. Trả về cặp Token mới an toàn.
        /// </summary>
        public async Task<ApiResult<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request)
        {
            // 1. Bóc tách Access Token (dù đã hết hạn) để lấy UserId
            // LƯU Ý NGHIỆP VỤ: Client gửi Access Token hết hạn cùng Refresh Token lên để yêu cầu gia hạn phiên làm việc
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

            // 2. Tìm User trong DB để đối chiếu
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return ApiResult<TokenResponse>.Fail("Người dùng không tồn tại.");
            }

            // 3. Kiểm tra tài khoản còn hoạt động không (trước khi check token)
            // LƯU Ý BẢO MẬT: Giúp phát hiện nhanh các tài khoản bị Quản trị viên vô hiệu hóa nóng trong lúc phiên làm việc đang chạy
            if (!user.IsActive)
            {
                var reason = !string.IsNullOrEmpty(user.LockReason) ? user.LockReason : "Vui lòng liên hệ quản trị viên.";
                return ApiResult<TokenResponse>.Fail($"Tài khoản đã bị khóa. Lý do: {reason}");
            }

            // 4. Kiểm tra Refresh Token có khớp và còn hạn không
            // So khớp mã băm SHA256 một chiều để bảo toàn tính xác thực
            var hashedToken = HashToken(request.RefreshToken);
            if (user.RefreshToken != hashedToken)
            {
                return ApiResult<TokenResponse>.Fail("Refresh Token không hợp lệ.");
            }

            if (user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            {
                return ApiResult<TokenResponse>.Fail("Refresh Token đã hết hạn. Vui lòng đăng nhập lại.");
            }

            // 5. Sinh cặp Token MỚI (Cơ chế Token Rotation)
            var newAccessToken = await GenerateJwtTokenAsync(user);
            var newRefreshToken = GenerateRefreshToken();

            // 6. Lưu Refresh Token mới vào DB (Vô hiệu hóa cái cũ) — BẰNG CONDITIONAL UPDATE.
            //
            // 🔴 VÌ SAO KHÔNG PHẢI `_userManager.UpdateAsync(user)` NHƯ TRƯỚC.
            // LoadProbe S09 đo được: 2 lời gọi refresh song song cùng một cặp token → **cả hai
            // đều 200**, mỗi bên nhận một refresh token khác nhau, nhưng DB chỉ giữ được MỘT
            // hash. Client thua cuộc cầm một token **đã chết ngay lúc nhận** và bị đá về trang
            // đăng nhập ở lần refresh kế tiếp — không ai thấy lỗi ở đâu cả.
            //
            // Đây là bối cảnh thật, không phải giả định: trang vừa tải,
            // `JwtAuthenticationStateProvider` và `AuthHeaderHandler` cùng phát hiện token hết
            // hạn. Single-flight phía client (`TokenRefreshCoordinator`, đợt 2) GIẤU được lỗi
            // này ở đường thường, nhưng không đóng được nó ở tầng server — hai tab, hai thiết
            // bị, hay `curl` vẫn đi vào đúng khe này.
            //
            // Câu UPDATE có vị từ `RefreshToken == hashedToken` đóng khe đó: kẻ thắng đổi hash,
            // kẻ thua khớp 0 dòng và **không được cấp token nào**. Một câu lệnh, không đọc lại,
            // không khoá tường minh — cùng khuôn với `ExecuteUpdateAsync` đã làm S02/S05 ĐẠT ở
            // đợt 1.
            //
            // ⚠️ Cố ý đi thẳng DbContext thay vì qua UserManager. `UpdateAsync` của Identity ghi
            // TOÀN BỘ entity và chốt bằng `ConcurrencyStamp` — mà mã cũ **bỏ luôn giá trị trả về
            // `IdentityResult`**, nên ngay cả chốt có sẵn đó cũng bị vứt đi trong im lặng. Ở đây
            // ta chỉ sửa hai cột của riêng ứng dụng (`RefreshToken`, `RefreshTokenExpiryTime`),
            // không phải cột nào của Identity, nên không có gì bị vượt qua.
            var refreshTokenExpirationDays = _configuration.GetValue<int>("JwtSettings:RefreshTokenExpirationDays");
            var newRefreshTokenHash = HashToken(newRefreshToken);
            var newRefreshTokenExpiry = DateTime.UtcNow.AddDays(refreshTokenExpirationDays);

            var rotated = await _context.Users
                .Where(u => u.Id == user.Id && u.RefreshToken == hashedToken)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(u => u.RefreshToken, newRefreshTokenHash)
                    .SetProperty(u => u.RefreshTokenExpiryTime, newRefreshTokenExpiry));

            if (rotated == 0)
            {
                // Một lời gọi khác đã xoay vòng token trước ta trong đúng khoảnh khắc này.
                // Trả lỗi RÕ RÀNG thay vì cấp một token đã chết: câu trả lời đúng cho client là
                // "đừng dùng token này", và đó chính là điều thông báo dưới đây nói.
                _logger.LogWarning(
                    "Xoay vòng refresh token thất bại do có lời gọi song song. UserId: {UserId}.",
                    user.Id);
                return ApiResult<TokenResponse>.Fail(
                    "Phiên đăng nhập vừa được làm mới bởi một yêu cầu khác. Vui lòng thử lại; nếu vẫn không được, hãy đăng nhập lại.");
            }

            return ApiResult<TokenResponse>.Ok(new TokenResponse
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken
            }, "Làm mới Token thành công.");
        }

        // =====================================================================
        // PRIVATE: Sinh JWT Access Token
        // =====================================================================
        /// <summary>
        /// NGHIỆP VỤ (BẢO MẬT): Khởi tạo mã truy cập dạng JWT Access Token cho phiên làm việc phi trạng thái (stateless session).
        /// Thu thập toàn bộ thông tin định danh và quyền hạn của người dùng, đóng gói vào Payload dưới dạng các Claims để Client gửi đính kèm trong mỗi yêu cầu API tiếp theo.
        /// </summary>
        private async Task<string> GenerateJwtTokenAsync(AppUser user)
        {
            // Đọc cấu hình từ appsettings.json — Đảm bảo tính linh động, KHÔNG BAO GIỜ HARDCODE khóa mật hoặc thời hạn
            var secretKey = _configuration["JwtSettings:SecretKey"]
                ?? throw new InvalidOperationException("JwtSettings:SecretKey chưa được cấu hình.");
            var issuer = _configuration["JwtSettings:Issuer"];
            var audience = _configuration["JwtSettings:Audience"];
            var expirationMinutes = _configuration.GetValue<int>("JwtSettings:AccessTokenExpirationMinutes");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Claims chuẩn bảo mật: NameIdentifier (UserId), Email, Name (Họ tên đầy đủ từ UserProfile)
            var profile = await _context.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == user.Id);
            var fullName = profile?.FullName ?? string.Empty;

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Email, user.Email ?? string.Empty),
                new(ClaimTypes.Name, fullName),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) // Thêm JTI định danh duy nhất cho từng Token chống trùng lặp
            };

            // Duyệt vòng lặp để nạp TOÀN BỘ Roles (Vai trò phân quyền) của User phục vụ Middleware Authorize trong ứng dụng
            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            // ⚠️ LƯU Ý BẢO MẬT CỰC KỲ QUAN TRỌNG: TUYỆT ĐỐI KHÔNG nhét Password hay thông tin nhạy cảm vào Claims vì dữ liệu JWT chỉ được mã hóa Base64 và có thể bị bóc mở dễ dàng ở phía Client!

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
        /// <summary>
        /// NGHIỆP VỤ (BẢO MẬT): Sinh chuỗi Refresh Token ngẫu nhiên có độ an toàn mã hóa cực cao.
        /// Sử dụng RandomNumberGenerator của hệ điều hành để tạo ra chuỗi byte ngẫu nhiên chất lượng mã hóa cao, loại bỏ hoàn toàn khả năng bị suy đoán số học.
        /// </summary>
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
        /// <summary>
        /// NGHIỆP VỤ (BẢO MẬT): Băm chuỗi Token bằng thuật toán SHA256 một chiều để lưu giữ an toàn trong DB.
        /// Sử dụng hàm băm một chiều SHA256 để lưu trữ token. Nếu cơ sở dữ liệu bị tấn công và rò rỉ dữ liệu, hacker cũng không thể dùng mã băm này để làm Refresh Token truy cập hệ thống.
        /// </summary>
        private static string HashToken(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToBase64String(bytes);
        }

        // =====================================================================
        // PRIVATE: Bóc tách Access Token đã hết hạn để lấy Claims
        // =====================================================================
        /// <summary>
        /// NGHIỆP VỤ (BẢO MẬT): Trích xuất thông tin định danh (Claims) từ Access Token đã hết hạn.
        /// Cấu hình tham số kiểm soát bắt buộc bỏ qua thời gian hết hạn (ValidateLifetime = false) để lấy lại UserId của phiên cũ phục vụ luồng Refresh Token,
        /// tuy nhiên vẫn duy trì kiểm định tính toàn vẹn chữ ký ký số (Signature) và thuật toán ký ban đầu (HmacSha256) nhằm phòng chống token giả.
        /// </summary>
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
                ValidateLifetime = false // ← Đặc biệt quan trọng: Chấp nhận token đã hết hạn thời gian hiệu lực
            };

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);

                // LƯU Ý BẢO MẬT: Kiểm tra thuật toán ký mã hóa có khớp HmacSha256 ban đầu hay không để chặn các cuộc tấn công thay đổi thuật toán chữ ký JWT (Algorithm Confusion Attack)
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
                return null; // Token không hợp lệ về mặt chữ ký hoặc cấu trúc đã bị thay đổi (tampered)
            }
        }

        // =====================================================================
        // CHANGE PASSWORD
        // =====================================================================
        public async Task<ApiResult<bool>> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return ApiResult<bool>.Fail("Không tìm thấy người dùng.");
            }

            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            if (!result.Succeeded)
            {
                var error = result.Errors.FirstOrDefault()?.Description ?? "Đổi mật khẩu thất bại.";
                return ApiResult<bool>.Fail("Mật khẩu hiện tại không đúng hoặc mật khẩu mới không hợp lệ.");
            }

            return ApiResult<bool>.Ok(true, "Đổi mật khẩu thành công.");
        }

        // =====================================================================
        // REGISTER (UC001: Khách hàng tự đăng ký)
        // =====================================================================
        /// <summary>
        /// NGHIỆP VỤ: Đăng ký tài khoản Khách hàng mới (UC001: Khách hàng tự đăng ký).
        /// Khởi tạo hồ sơ định danh duy nhất cho khách hàng, thiết lập bảo mật mật khẩu, hồ sơ chi tiết cá nhân và phân quyền mặc định.
        /// Quy trình nghiệp vụ:
        /// 1. Kiểm tra tính duy nhất của địa chỉ Email trên toàn hệ thống để tránh trùng lặp tài khoản đăng nhập.
        /// 2. Kiểm tra tính duy nhất của Số điện thoại trong cơ sở dữ liệu để làm cơ sở liên hệ giao nhận hàng hóa chuẩn xác.
        /// 3. Khởi tạo thực thể AppUser: mặc định kích hoạt (IsActive = true), loại tài khoản Customer (Type = 2), và lưu thời gian tạo tài khoản.
        /// 4. Sử dụng ASP.NET Core Identity để băm mật khẩu bảo mật trước khi ghi nhận lưu trữ vật lý vào cơ sở dữ liệu.
        /// 5. Tạo bản ghi hồ sơ chi tiết (UserProfile) để lưu trữ các thông tin hiển thị (Họ và Tên) liên kết trực tiếp tới tài khoản mới.
        /// 6. Gán vai trò (Role) mặc định "Customer" để người dùng có đầy đủ quyền thao tác mua sắm và quản lý đơn hàng của riêng mình trên hệ thống.
        /// 7. Trả về kết quả hoàn tất đăng ký tài khoản thành công.
        /// </summary>
        public async Task<ApiResult<bool>> RegisterAsync(RegisterCustomerRequest request)
        {
            // 1. Kiểm tra Email đã tồn tại chưa
            // LƯU Ý NGHIỆP VỤ: Email đóng vai trò định danh đăng nhập bắt buộc duy nhất
            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null)
            {
                return ApiResult<bool>.Fail("Email này đã được sử dụng.");
            }

            // 2. Kiểm tra SĐT đã tồn tại chưa
            // LƯU Ý NGHIỆP VỤ: Ràng buộc duy nhất số điện thoại phục vụ mục đích bảo mật tài khoản và xác minh giao dịch
            var existingPhone = _context.Users.Any(u => u.PhoneNumber == request.PhoneNumber);
            if (existingPhone)
            {
                return ApiResult<bool>.Fail("Số điện thoại này đã được sử dụng.");
            }

            // 3. Tạo AppUser mới
            var user = new AppUser
            {
                Id = Guid.NewGuid(),
                UserName = request.Email,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                IsActive = true,
                Type = 2, // Phân loại tài khoản: 2 tương ứng với loại Customer (Khách mua hàng)
                CreatedDate = DateTime.UtcNow,
                IsDeleted = false
            };

            // Thực thi tạo tài khoản cùng thuật toán băm mật khẩu nội bộ của Identity
            var createResult = await _userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                return ApiResult<bool>.Fail("Đăng ký thất bại: " + errors);
            }

            // 4. Tạo UserProfile liên kết để lưu thông tin phi định danh hiển thị
            _context.UserProfiles.Add(new UserProfile
            {
                UserId = user.Id,
                FullName = request.FullName.Trim()
            });
            await _context.SaveChangesAsync();

            // 5. Gán vai trò (Assign role) mặc định cho tài khoản
            // LƯU Ý NGHIỆP VỤ: Thiết lập quyền cơ bản nhất để bảo vệ các tài nguyên hệ thống, chỉ cấp phép những API thuộc nhóm Customer
            await _userManager.AddToRoleAsync(user, "Customer");

            return ApiResult<bool>.Ok(true, "Đăng ký tài khoản thành công. Vui lòng đăng nhập.");
        }
    }
}
