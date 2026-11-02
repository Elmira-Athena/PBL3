using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using PBL3.API.Middleware;

const string bridge = "172.17.0.1";
const string cloudflareChain = "198.51.100.10, 173.245.48.15";
var cases = new ProbeCase[]
{
    new("direct ALB ignores forged XFF prefix", false, null, "192.0.2.250, 198.51.100.10", "198.51.100.10"),
    new("direct ALB ignores forged CF header", false, ["192.0.2.250"], "198.51.100.10", "198.51.100.10"),
    new("direct traffic without XFF ignores CF header", false, ["192.0.2.250"], null, bridge),
    new("direct ALB IPv6 with client port", false, null, "192.0.2.250, [2001:db8::10]:54321", "2001:db8::10"),
    new("locked Cloudflare uses visitor rather than edge", true, ["198.51.100.10"], cloudflareChain, "198.51.100.10"),
    new("second visitor behind same edge has own partition", true, ["198.51.100.11"], "198.51.100.11, 173.245.48.15", "198.51.100.11"),
    new("forged XFF cannot override trusted CF visitor", true, ["198.51.100.10"], "192.0.2.250, 173.245.48.15", "198.51.100.10"),
    new("locked Cloudflare IPv6 visitor", true, ["2001:db8::10"], cloudflareChain, "2001:db8::10"),
    new("locked Cloudflare IPv4-mapped IPv6 visitor", true, ["::ffff:198.51.100.10"], cloudflareChain, "::ffff:198.51.100.10"),
    new("missing CF header fails closed despite valid XFF", true, null, cloudflareChain, null, 400),
    new("invalid CF header fails closed", true, ["garbage"], cloudflareChain, null, 400),
    new("empty CF header fails closed", true, [""], cloudflareChain, null, 400),
    new("whitespace CF header fails closed", true, [" "], cloudflareChain, null, 400),
    new("multiple CF IPs fail closed", true, ["198.51.100.10, 198.51.100.11"], cloudflareChain, null, 400),
    new("repeated CF headers fail closed", true, ["198.51.100.10", "198.51.100.11"], cloudflareChain, null, 400),
    new("duplicate identical CF headers fail closed", true, ["198.51.100.10", "198.51.100.10"], cloudflareChain, null, 400),
    new("CF header with port fails closed", true, ["198.51.100.10:443"], cloudflareChain, null, 400),
    new("quoted CF header fails closed", true, ["\"198.51.100.10\""], cloudflareChain, null, 400),
    new("ALB readiness without CF header stays available", true, null, null, bridge, Path: "/health/ready"),
    new("Docker liveness without CF header stays available", true, null, null, bridge, Path: "/health/live", Scheme: "http"),
    new("legacy health without CF header stays available", true, null, null, bridge, Path: "/health"),
    new("HEAD readiness without CF header stays available", true, null, null, bridge, Path: "/health/ready", Method: "HEAD"),
    new("health POST is not a header exemption", true, null, null, null, 400, Path: "/health/ready", Method: "POST"),
    new("unknown health path is not a header exemption", true, null, null, null, 400, Path: "/health/unknown"),
    new("health-like prefix is not a header exemption", true, null, null, null, 400, Path: "/healthy"),
    new("malformed CF header on health fails closed", true, ["garbage"], null, null, 400, Path: "/health/ready"),
};

var failures = new List<string>();
foreach (var test in cases)
{
    var services = new ServiceCollection();
    services.AddLogging();
    services.Configure<ForwardedHeadersOptions>(options => ProxyHeaders.Configure(options, test.Lockdown));
    using var provider = services.BuildServiceProvider();
    var app = new ApplicationBuilder(provider);
    app.UseTrustedProxyHeaders(test.Lockdown);
    var reachedEndpoint = false;
    app.Run(_ =>
    {
        reachedEndpoint = true;
        return Task.CompletedTask;
    });

    var context = new DefaultHttpContext();
    context.RequestServices = provider;
    context.Connection.RemoteIpAddress = IPAddress.Parse(bridge);
    context.Request.Method = test.Method;
    context.Request.Path = test.Path;
    context.Request.Scheme = "http";
    context.Request.Headers["X-Forwarded-Proto"] = test.Scheme;
    if (test.Xff is not null)
        context.Request.Headers["X-Forwarded-For"] = test.Xff;
    if (test.Cf is not null)
        context.Request.Headers["CF-Connecting-IP"] = new StringValues(test.Cf);

    await app.Build()(context);
    Check(context.Response.StatusCode == test.Status,
        $"status expected {test.Status}, got {context.Response.StatusCode}");
    Check(reachedEndpoint == (test.Status == 200), "endpoint reachability differs");
    if (test.Status == 200)
    {
        Check(context.Connection.RemoteIpAddress?.ToString() == test.Ip,
            $"IP expected {test.Ip}, got {context.Connection.RemoteIpAddress}");
        Check(context.Request.Scheme == test.Scheme,
            $"scheme expected {test.Scheme}, got {context.Request.Scheme}");
    }

    void Check(bool condition, string message)
    {
        if (!condition)
            failures.Add($"{test.Name}: {message}");
    }
}

foreach (var failure in failures)
    Console.Error.WriteLine($"FAIL {failure}");
if (failures.Count > 0)
    throw new InvalidOperationException($"{failures.Count} assertion(s) failed across {cases.Length} cases.");
Console.WriteLine($"PASS {cases.Length} proxy-header cases (real ForwardedHeadersMiddleware; no network or NuGet dependencies).");

internal sealed record ProbeCase(
    string Name, bool Lockdown, string[]? Cf, string? Xff, string? Ip,
    int Status = 200, string Path = "/api/products", string Method = "GET", string Scheme = "https");
