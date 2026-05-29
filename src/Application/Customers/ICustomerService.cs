using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Customers;
using PBL3.Shared.DTOs.Products;
using System;
using System.Threading.Tasks;

namespace PBL3.Application.Customers
{
    public interface ICustomerService
    {
        Task<ApiResult<PagedResult<CustomerDto>>> GetPagedListAsync(CustomerFilterRequest filter);
        Task<ApiResult<CustomerDetailDto>> GetByIdAsync(Guid id);
        Task<ApiResult<CustomerDto>> CreateAsync(CreateCustomerRequest request);
        Task<ApiResult<CustomerDto>> UpdateAsync(Guid id, UpdateCustomerRequest request);
        Task<ApiResult<bool>> DeactivateAsync(Guid id, string? lockReason);
        Task<ApiResult<bool>> ReactivateAsync(Guid id);
    }
}
