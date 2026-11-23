using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace PBL3.API.Middleware;

public static class ProxyHeaders
{
    // https://www.cloudflare.com/ips/ (lấy 2026-11-23).
    // ponytail: danh sách tĩnh; Cloudflare thêm dải mới thì request từ dải đó quay về hành vi cũ
    // (IP edge làm khoá rate limit) chứ không mở lỗ giả mạo — cập nhật khi CF công bố dải mới.
    private static readonly string[] CloudflareRanges =
    [
        "173.245.48.0/20", "103.21.244.0/22", "103.22.200.0/22", "103.31.4.0/22",
        "141.101.64.0/18", "108.162.192.0/18", "190.93.240.0/20", "188.114.96.0/20",
        "197.234.240.0/22", "198.41.128.0/17", "162.158.0.0/15", "104.16.0.0/13",
        "104.24.0.0/14", "172.64.0.0/13", "131.0.72.0/22",
        "2400:cb00::/32", "2606:4700::/32", "2803:f800::/32", "2405:b500::/32",
        "2405:8100::/32", "2a06:98c0::/29", "2c0f:f248::/32",
    ];

    public static void Configure(ForwardedHeadersOptions options)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        // Peer trực tiếp của app là container WAF trên Docker bridge; sg-web chỉ nhận traffic từ ALB.
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("172.16.0.0/12"));
        foreach (var range in CloudflareRanges)
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(range));
        // Hop 1 = entry ALB ghi (peer thật của ALB). Chỉ khi hop đó là edge Cloudflare thì mới đọc
        // hop 2 (client do Cloudflare ghi); peer không thuộc dải CF thì middleware dừng ở hop 1, nên
        // XFF client tự gửi thẳng vào ALB vẫn không được tin.
        // Không bao giờ nâng lên 3: hop 3 là do client tự khai.
        options.ForwardLimit = 2;
    }
}
