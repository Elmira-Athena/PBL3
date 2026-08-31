using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PBL3.Infrastructure.Data;

namespace PBL3.Tools.LoadProbe.Infra;

/// <summary>
/// Mọi thứ một kịch bản cần: mở DbContext mới, bắn HTTP, tự ký token.
/// </summary>
public sealed class ProbeEnvironment : IDisposable
{
    private readonly ProbeConfig _config;
    private readonly HttpClient _http;

    public ProbeEnvironment(ProbeConfig config)
    {
        _config = config;
        _http = new HttpClient(new SocketsHttpHandler
        {
            // Mặc định của SocketsHttpHandler đã là không giới hạn, nhưng ghi rõ ra
            // để người đọc sau không phải tra: nếu đặt nhầm số nhỏ thì "đồng thời"
            // biến thành "xếp hàng" và mọi kịch bản đều PASS một cách vô nghĩa.
            MaxConnectionsPerServer = 256,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        })
        {
            BaseAddress = new Uri(_config.ApiBaseUrl + "/"),
            Timeout = TimeSpan.FromSeconds(60)
        };
    }

    public ProbeConfig Config => _config;

    // ── DB ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// LUÔN mở context MỚI cho mỗi lần khẳng định.
    ///
    /// Dùng lại context đã seed là cái bẫy im lặng số một của công cụ kiểu này:
    /// Change Tracker trả về thực thể trong RAM chứ không đọc lại DB, nên bất biến
    /// được kiểm trên đúng dữ liệu mà probe vừa tự ghi — luôn đúng, không bao giờ
    /// bắt được lỗi.
    /// </summary>
    public HushStoreDbContext NewDbContext()
    {
        var options = new DbContextOptionsBuilder<HushStoreDbContext>()
            .UseSqlServer(_config.ConnectionString, sql => sql.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null))
            .Options;

        return new HushStoreDbContext(options);
    }

    // ── HTTP ──────────────────────────────────────────────────────────────────

    public async Task<HttpResponseMessage> PostAsync<TBody>(string path, TBody body, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body)
        };
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return await _http.SendAsync(request);
    }

    public async Task<HttpResponseMessage> PostEmptyAsync(string path, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return await _http.SendAsync(request);
    }

    public async Task<bool> IsApiUpAsync()
    {
        try
        {
            // /health/* được miễn trừ rate limiter một cách có chủ đích, nên thăm dò
            // ở đây không đốt hạn mức của kịch bản sắp chạy.
            using var response = await _http.GetAsync("health/live");
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    // ── Token ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Tự ký access token thay vì gọi /api/auth/login.
    ///
    /// LÝ DO BẮT BUỘC, không phải tiện tay: đăng nhập bị giới hạn 5 lần/phút mỗi IP
    /// và đăng ký 3 lần/giờ. Kịch bản "50 khách checkout đồng thời" mà đi qua login
    /// thì 45 khách ăn 429 trước khi chạm tới code cần đo.
    ///
    /// An toàn vì probe dùng ĐÚNG khoá ký mà API đang chạy dùng — nó không vòng qua
    /// lớp xác thực, nó đóng vai người đã đăng nhập hợp lệ. Middleware kiểm IsActive
    /// đọc thẳng DB nên vẫn chạy đầy đủ.
    /// </summary>
    public string MintToken(Guid userId, string email, params string[] roles)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config.JwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var token = new JwtSecurityToken(
            issuer: _config.JwtIssuer,
            audience: _config.JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public void Dispose() => _http.Dispose();
}
