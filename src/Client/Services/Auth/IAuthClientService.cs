using PBL3.Shared.DTOs.Auth;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Customers;

namespace Client.Services.Auth
{
    public interface IAuthClientService
    {
        Task<ApiResult<TokenResponse>> LoginAsync(LoginRequest request);
        Task LogoutAsync();
        Task<ApiResult<bool>> RegisterAsync(RegisterCustomerRequest request);
    }
}
