using System.Security.Claims;
using System.Text.Json;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;

namespace Client.Auth;

/// <summary>
/// Custom AuthenticationStateProvider cho Blazor WASM.
/// Đọc JWT từ LocalStorage, decode Base64 Payload để trích xuất Claims.
/// LƯU Ý: Class này chỉ phục vụ UX (ẩn/hiện menu), KHÔNG có tác dụng bảo mật.
/// Mọi bảo mật thực sự đều nằm ở API Backend.
/// </summary>
public class JwtAuthenticationStateProvider(ILocalStorageService localStorage) : AuthenticationStateProvider
{
    private readonly ILocalStorageService _localStorage = localStorage;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _localStorage.GetItemAsStringAsync(TokenKey);

        // Nếu không có token hoặc token rỗng -> Trạng thái "Chưa đăng nhập"
        if (string.IsNullOrWhiteSpace(token))
        {
            return new AuthenticationState(
                new ClaimsPrincipal(new ClaimsIdentity()));
        }

        // Loại bỏ dấu ngoặc kép nếu LocalStorage trả về chuỗi có bọc quotes
        token = token.Trim('"');

        // Parse claims từ JWT payload
        var claims = ParseClaimsFromJwt(token);

        // Tạo ClaimsIdentity với scheme "jwt" để Blazor biết user đã authenticated
        var identity = new ClaimsIdentity(claims, "jwt");
        var user = new ClaimsPrincipal(identity);

        return new AuthenticationState(user);
    }

    /// <summary>
    /// Thông báo cho Blazor rằng trạng thái auth đã thay đổi (gọi sau Login/Logout).
    /// </summary>
    public void NotifyAuthStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    /// <summary>
    /// Parse danh sách Claims từ JWT token string.
    /// JWT có 3 phần: Header.Payload.Signature, ta chỉ cần decode phần Payload (index 1).
    /// </summary>
    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var claims = new List<Claim>();

        // JWT = header.payload.signature
        var parts = jwt.Split('.');
        if (parts.Length != 3)
        {
            // Token không hợp lệ (không đúng format 3 phần) -> trả về rỗng
            return claims;
        }

        var payload = parts[1];

        // Decode Base64Url -> JSON bytes
        var jsonBytes = DecodeBase64Url(payload);
        if (jsonBytes == null || jsonBytes.Length == 0)
        {
            return claims;
        }

        // Parse JSON payload thành dictionary
        var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonBytes);
        if (keyValuePairs == null)
        {
            return claims;
        }

        // Xử lý claim "role" đặc biệt (có thể là string hoặc array)
        if (keyValuePairs.TryGetValue(ClaimTypes.Role, out var roles))
        {
            if (roles.ValueKind == JsonValueKind.Array)
            {
                foreach (var role in roles.EnumerateArray())
                {
                    claims.Add(new Claim(ClaimTypes.Role, role.GetString()!));
                }
            }
            else
            {
                claims.Add(new Claim(ClaimTypes.Role, roles.GetString()!));
            }

            keyValuePairs.Remove(ClaimTypes.Role);
        }

        // Map các claims còn lại
        foreach (var kvp in keyValuePairs)
        {
            // Bỏ qua các claim hệ thống JSON không cần thiết (exp, iat, nbf, iss, aud)
            // vẫn giữ lại để Blazor có thể dùng nếu cần
            claims.Add(new Claim(kvp.Key, kvp.Value.ToString()));
        }

        return claims;
    }

    /// <summary>
    /// Decode chuỗi Base64Url thành byte array.
    /// 
    /// QUAN TRỌNG - Xử lý padding:
    /// Base64 chuẩn yêu cầu độ dài chuỗi chia hết cho 4. JWT dùng Base64Url 
    /// (thay '+' bằng '-', thay '/' bằng '_') và LOẠI BỎ padding '='.
    /// 
    /// Nếu không thêm lại padding, Convert.FromBase64String() sẽ ném FormatException.
    /// 
    /// Công thức: Thêm (4 - length % 4) % 4 ký tự '=' vào cuối.
    /// - length % 4 == 0 -> thêm 0 ký tự (đã đủ)
    /// - length % 4 == 1 -> KHÔNG HỢP LỆ trong Base64 (nhưng ta vẫn thêm 3 để tránh crash)
    /// - length % 4 == 2 -> thêm 2 ký tự '=='
    /// - length % 4 == 3 -> thêm 1 ký tự '='
    /// </summary>
    private static byte[]? DecodeBase64Url(string base64Url)
    {
        try
        {
            // Bước 1: Thay thế ký tự Base64Url -> Base64 chuẩn
            var base64 = base64Url
                .Replace('-', '+')   // Base64Url dùng '-' thay cho '+'
                .Replace('_', '/');  // Base64Url dùng '_' thay cho '/'

            // Bước 2: Thêm padding '=' cho đủ bội số của 4
            switch (base64.Length % 4)
            {
                case 2:
                    base64 += "==";
                    break;
                case 3:
                    base64 += "=";
                    break;
                case 0:
                    // Đã đủ padding, không cần thêm
                    break;
                default:
                    // length % 4 == 1: Chuỗi Base64 không hợp lệ
                    // Trả về null để caller xử lý gracefully thay vì ném exception
                    return null;
            }

            // Bước 3: Decode thành byte[]
            return Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            // Nếu vẫn lỗi format (chuỗi chứa ký tự không hợp lệ) -> trả về null
            // Không ném exception ra ngoài để tránh crash app
            return null;
        }
    }
}
