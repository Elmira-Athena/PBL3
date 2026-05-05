using System.Collections.Generic;
using System.Threading.Tasks;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Customers;

namespace Client.Services.Customer
{
    public interface IUserAddressClientService
    {
        Task<ApiResult<List<UserAddressDto>>> GetMyAddressesAsync();
        Task<ApiResult<int>> AddAddressAsync(UserAddressDto request);
    }
}
