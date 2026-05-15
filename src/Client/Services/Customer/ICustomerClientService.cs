using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Customers;
using PBL3.Shared.DTOs.Products;

namespace Client.Services.Customer
{
    public interface ICustomerClientService
    {
        Task<ApiResult<PagedResult<CustomerDto>>> GetListAsync(CustomerFilterRequest request);
        Task<ApiResult<CustomerDetailDto>> GetByIdAsync(Guid id);
        Task<ApiResult<CustomerDto>> CreateAsync(CreateCustomerRequest request);
        Task<ApiResult<CustomerDto>> UpdateAsync(Guid id, UpdateCustomerRequest request);
        Task<ApiResult<bool>> DeactivateAsync(Guid id);
        Task<ApiResult<CustomerDto>> GetMyProfileAsync();
        Task<ApiResult<CustomerDto>> UpdateMyProfileAsync(UpdateCustomerRequest request);
    }
}
