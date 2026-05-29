using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using PBL3.Core.Entities;
using PBL3.Shared.DTOs.Common;

namespace PBL3.API.Middlewares;

public class UserStatusMiddleware
{
    private readonly RequestDelegate _next;

    public UserStatusMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            !context.Request.Path.StartsWithSegments("/api/auth"))
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                var cache = context.RequestServices.GetRequiredService<IMemoryCache>();
                var cacheKey = $"user_isactive_{userId.ToLowerInvariant()}";

                if (!cache.TryGetValue(cacheKey, out (bool IsActive, string? LockReason) userData))
                {
                    var userManager = context.RequestServices.GetRequiredService<UserManager<AppUser>>();
                    var user = await userManager.FindByIdAsync(userId);
                    userData = (user?.IsActive ?? false, user?.LockReason);
                    cache.Set(cacheKey, userData, TimeSpan.FromSeconds(30));
                }

                if (!userData.IsActive)
                {
                    var reason = !string.IsNullOrEmpty(userData.LockReason)
                        ? userData.LockReason
                        : "Vui lòng liên hệ quản trị viên.";
                    var message = $"Tài khoản của bạn đã bị khóa. Lý do: {reason}";
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json";
                    context.Response.Headers["X-Account-Status"] = "locked";
                    await context.Response.WriteAsJsonAsync(ApiResult<object>.Fail(message));
                    return;
                }
            }
        }
        await _next(context);
    }
}
