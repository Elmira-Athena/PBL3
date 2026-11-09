using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace PBL3.API.Middleware;

public static class ProxyHeaders
{
    public static void Configure(ForwardedHeadersOptions options)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        // Bridge mode hides ALB IPs; sg-web allows only ALB traffic.
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        // ALB appends to XFF; consuming a second entry would trust client input.
        options.ForwardLimit = 1;
    }
}
