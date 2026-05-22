using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Employees;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Client.Services.Employee
{
    public interface IEmployeeClientService
    {
        Task<ApiResult<PagedResult<EmployeeListDto>>> GetListAsync(EmployeeFilterRequest request);
        Task<ApiResult<EmployeeListDto>> GetByIdAsync(Guid id);
        Task<ApiResult<EmployeeListDto>> CreateAsync(CreateEmployeeRequest request);
        Task<ApiResult<EmployeeListDto>> UpdateAsync(Guid id, UpdateEmployeeRequest request);
        Task<ApiResult<bool>> DeactivateAsync(Guid id, string? lockReason = null);
        Task<ApiResult<bool>> ReactivateAsync(Guid id);
        Task<ApiResult<List<EmployeeDto>>> GetTechniciansAsync();
    }
}
