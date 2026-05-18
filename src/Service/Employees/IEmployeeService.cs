using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Employees;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PBL3.Service.Employees
{
    public interface IEmployeeService
    {
        Task<ApiResult<PagedResult<EmployeeListDto>>> GetPagedListAsync(EmployeeFilterRequest filter);
        Task<ApiResult<EmployeeListDto>> GetByIdAsync(Guid id);
        Task<ApiResult<EmployeeListDto>> CreateAsync(CreateEmployeeRequest request);
        Task<ApiResult<EmployeeListDto>> UpdateAsync(Guid id, UpdateEmployeeRequest request);
        Task<ApiResult<bool>> DeactivateAsync(Guid id, string? lockReason);
        Task<ApiResult<bool>> ReactivateAsync(Guid id);
        Task<ApiResult<List<EmployeeDto>>> GetTechniciansSimpleAsync();
    }
}
