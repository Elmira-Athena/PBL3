using PBL3.Shared.DTOs.Auth;
using PBL3.Shared.DTOs.Common;

namespace Client.Services
{
    public interface IAuthClientService
    {
        Task<ApiResult<TokenResponse>> LoginAsync(LoginRequest request);
        Task LogoutAsync();
    }
}
