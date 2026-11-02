using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using PBL3.Shared.DTOs.Common;

namespace PBL3.API.Middleware;

public static class ProxyHeaders
{
    private const string CloudflareVisitorHeader = "CF-Connecting-IP";

    public static void Configure(ForwardedHeadersOptions options, bool cloudflareOriginLockdown)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        // Bridge mode hides ALB IPs; sg-web allows only ALB traffic.
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        // ALB appends to XFF; consuming a second entry would trust client input.
        options.ForwardLimit = 1;
        if (cloudflareOriginLockdown)
        {
            // Requires Cloudflare-only ALB ingress AND the origin-secret listener rule.
            options.ForwardedForHeaderName = CloudflareVisitorHeader;
        }
    }

    public static IApplicationBuilder UseTrustedProxyHeaders(
        this IApplicationBuilder app, bool cloudflareOriginLockdown)
    {
        if (cloudflareOriginLockdown)
        {
            app.Use(async (context, next) =>
            {
                var headers = context.Request.Headers;
                var visitor = headers[CloudflareVisitorHeader];
                var healthCheckWithoutHeader = !headers.ContainsKey(CloudflareVisitorHeader)
                    && IsHealthCheck(context.Request);
                // ForwardedHeaders silently keeps the bridge IP on invalid input.
                if (!healthCheckWithoutHeader
                    && (visitor.Count != 1 || !IPAddress.TryParse(visitor[0], out _)))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsJsonAsync(
                        ApiResult<object>.Fail("Thông tin địa chỉ truy cập không hợp lệ."),
                        ErrorResponseJson.Options);
                    return;
                }

                await next(context);
            });
        }

        return app.UseForwardedHeaders();
    }

    private static bool IsHealthCheck(HttpRequest request)
        => (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
           && (request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase)
               || request.Path.Equals("/health/live", StringComparison.OrdinalIgnoreCase)
               || request.Path.Equals("/health/ready", StringComparison.OrdinalIgnoreCase));
}
