using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using PBL3.Core.Entities;
using PBL3.Shared.DTOs.Common;

namespace PBL3.API.Middlewares;

/// <summary>
/// Middleware kiểm tra trạng thái hoạt động của tài khoản người dùng.
///
/// Vấn đề cần giải quyết:
///   JWT Token có thể được cấp từ TRƯỚC khi Admin khóa tài khoản.
///   Middleware UseAuthentication() chỉ kiểm tra chữ ký và hạn dùng của Token,
///   nên Token cũ vẫn hợp lệ dù tài khoản đã bị khóa.
///   Middleware này bổ sung thêm kiểm tra thời gian thực từ DB.
///
/// Vị trí trong pipeline (Program.cs):
///   UseAuthentication()              ← giải mã JWT, điền context.User
///   UseMiddleware<UserStatusMiddleware>()  ← middleware này chạy ở đây
///   UseAuthorization()               ← kiểm tra Role
/// </summary>
public class UserStatusMiddleware
{
    // _next đại diện cho middleware kế tiếp trong pipeline.
    // Gọi _next(context) nghĩa là "chuyển request xuống tầng tiếp theo".
    private readonly RequestDelegate _next;

    public UserStatusMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Điều kiện kép để quyết định có cần kiểm tra không:
        //   1. IsAuthenticated == true  → JWT đã được UseAuthentication() giải mã thành công
        //                                  (Token hợp lệ, chưa hết hạn, chữ ký đúng)
        //   2. Path không bắt đầu bằng /api/auth → bỏ qua các endpoint Login/Register
        //      vì tại thời điểm đó user chưa có Token, kiểm tra sẽ vô nghĩa.
        if (context.User.Identity?.IsAuthenticated == true &&
            !context.Request.Path.StartsWithSegments("/api/auth"))
        {
            // Lấy UserId từ Claims trong JWT (claim "sub" / NameIdentifier).
            // UseAuthentication() đã trích xuất sẵn vào context.User rồi.
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                // ── TỐI ƯU HIỆU NĂNG: Dùng MemoryCache thay vì query DB mỗi request ──
                // Mỗi API call đều phải qua middleware này. Nếu mỗi lần đều query DB
                // thì với 1000 request/giây sẽ phát sinh 1000 câu SELECT/giây — lãng phí.
                // Cache giữ kết quả trong 30 giây: trong khoảng thời gian đó dùng kết quả cũ.
                // Hệ quả chấp nhận được: Sau khi Admin khóa, user tối đa còn dùng được 30 giây.
                var cache = context.RequestServices.GetRequiredService<IMemoryCache>();
                var cacheKey = $"user_isactive_{userId.ToLowerInvariant()}";

                // TryGetValue: thử lấy từ cache.
                //   - Nếu có (cache hit)  → userData được điền, bỏ qua khối if bên dưới.
                //   - Nếu không (cache miss) → vào khối if để query DB và lưu vào cache.
                if (!cache.TryGetValue(cacheKey, out (bool IsActive, string? LockReason) userData))
                {
                    // Cache miss → lấy UserManager từ DI container để truy vấn DB.
                    // Lấy từ RequestServices (Scoped) thay vì constructor (Singleton)
                    // vì UserManager là Scoped — không thể inject vào Singleton middleware.
                    var userManager = context.RequestServices.GetRequiredService<UserManager<AppUser>>();
                    var user = await userManager.FindByIdAsync(userId);

                    // Lưu trạng thái vào tuple (IsActive, LockReason).
                    // Nếu user không tồn tại (đã bị xóa) → mặc định IsActive = false.
                    userData = (user?.IsActive ?? false, user?.LockReason);

                    // Lưu vào cache với TTL 30 giây để các request tiếp theo dùng lại.
                    cache.Set(cacheKey, userData, TimeSpan.FromSeconds(30));
                }

                // ── KIỂM TRA TRẠNG THÁI TÀI KHOẢN ──
                if (!userData.IsActive)
                {
                    // Dùng LockReason từ DB nếu có, ngược lại hiển thị thông báo mặc định.
                    var reason = !string.IsNullOrEmpty(userData.LockReason)
                        ? userData.LockReason
                        : "Vui lòng liên hệ quản trị viên.";
                    var message = $"Tài khoản của bạn đã bị khóa. Lý do: {reason}";

                    // Trả về HTTP 403 Forbidden (khác 401 Unauthorized:
                    //   401 = chưa xác thực / không biết bạn là ai
                    //   403 = đã biết bạn là ai, nhưng không cho phép).
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json";

                    // Header tuỳ chỉnh để Blazor Client phân biệt lý do bị từ chối.
                    // Header này được expose qua CORS trong Program.cs:
                    //   .WithExposedHeaders("X-Account-Status")
                    context.Response.Headers["X-Account-Status"] = "locked";

                    // Ghi phản hồi JSON theo chuẩn ApiResult của dự án.
                    await context.Response.WriteAsJsonAsync(ApiResult<object>.Fail(message));

                    // return sớm = DỪNG pipeline tại đây, KHÔNG gọi _next(context).
                    // Request sẽ không bao giờ xuống tới Controller.
                    return;
                }
            }
        }

        // Tài khoản hợp lệ (hoặc không cần kiểm tra) → chuyển request xuống middleware tiếp theo.
        await _next(context);
    }
}

