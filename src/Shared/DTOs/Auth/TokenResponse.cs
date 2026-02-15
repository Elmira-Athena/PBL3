namespace PBL3.Shared.DTOs.Auth
{
    /// <summary>
    /// DTO trả về cặp Access Token + Refresh Token cho Client.
    /// KHÔNG chứa bất kỳ thông tin nhạy cảm nào (Password, v.v.).
    /// </summary>
    public class TokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }
}
