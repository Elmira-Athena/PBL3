using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using PBL3.API.Middleware;

const string bridge = "172.17.0.1";
var cases = new ProbeCase[]
{
    new("direct ALB ignores forged XFF prefix", null, "192.0.2.250, 198.51.100.10", "198.51.100.10"),
    new("direct ALB ignores forged CF header", ["192.0.2.250"], "198.51.100.10", "198.51.100.10"),
    new("direct traffic without XFF ignores CF header", ["192.0.2.250"], null, bridge),
    new("direct ALB IPv4 with client port", null, "192.0.2.250, 198.51.100.10:54321", "198.51.100.10"),
    new("direct ALB IPv6 with client port", null, "192.0.2.250, [2001:db8::10]:54321", "2001:db8::10"),
    new("direct ALB IPv4-mapped IPv6", null, "192.0.2.250, ::ffff:198.51.100.10", "::ffff:198.51.100.10"),
    new("invalid XFF retains bridge IP and original scheme", ["192.0.2.250"], "garbage", bridge, Scheme: "http"),
    new("invalid CF header is ignored", ["garbage"], "198.51.100.10", "198.51.100.10"),
    new("repeated CF headers are ignored", ["192.0.2.250", "192.0.2.251"], "198.51.100.10", "198.51.100.10"),
    new("direct ALB HTTP scheme", null, "198.51.100.10", "198.51.100.10", Proto: "http", Scheme: "http"),
    new("missing forwarded scheme retains HTTP", null, "198.51.100.10", "198.51.100.10", Proto: null, Scheme: "http"),
    new("missing proxy headers retain bridge IP and HTTP", null, null, bridge, Proto: null, Scheme: "http"),
};

var failures = new List<string>();
foreach (var test in cases)
{
    var services = new ServiceCollection();
    services.AddLogging();
    services.Configure<ForwardedHeadersOptions>(ProxyHeaders.Configure);
    using var provider = services.BuildServiceProvider();
    var app = new ApplicationBuilder(provider);
    app.UseForwardedHeaders();
    var reachedEndpoint = false;
    app.Run(_ =>
    {
        reachedEndpoint = true;
        return Task.CompletedTask;
    });

    var context = new DefaultHttpContext();
    context.RequestServices = provider;
    context.Connection.RemoteIpAddress = IPAddress.Parse(bridge);
    context.Request.Scheme = "http";
    if (test.Proto is not null)
        context.Request.Headers["X-Forwarded-Proto"] = test.Proto;
    if (test.Xff is not null)
        context.Request.Headers["X-Forwarded-For"] = test.Xff;
    if (test.Cf is not null)
        context.Request.Headers["CF-Connecting-IP"] = new StringValues(test.Cf);

    await app.Build()(context);
    Check(context.Response.StatusCode == 200,
        $"status expected 200, got {context.Response.StatusCode}");
    Check(reachedEndpoint, "endpoint was not reached");
    Check(context.Connection.RemoteIpAddress?.ToString() == test.Ip,
        $"IP expected {test.Ip}, got {context.Connection.RemoteIpAddress}");
    Check(context.Request.Scheme == test.Scheme,
        $"scheme expected {test.Scheme}, got {context.Request.Scheme}");

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
    string Name, string[]? Cf, string? Xff, string Ip,
    string? Proto = "https", string Scheme = "https");
