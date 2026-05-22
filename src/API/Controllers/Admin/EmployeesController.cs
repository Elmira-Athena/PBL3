using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBL3.Service.Employees;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Employees;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PBL3.API.Controllers.Admin
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class EmployeesController : ControllerBase
    {
        private readonly IEmployeeService _employeeService;

        public EmployeesController(IEmployeeService employeeService)
        {
            _employeeService = employeeService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ApiResult<PagedResult<EmployeeListDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetList(
            [FromQuery] string? keyword,
            [FromQuery] bool? isActive,
            [FromQuery] byte? gender,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? sortBy = null,
            [FromQuery] bool sortDescending = true)
        {
            var filter = new EmployeeFilterRequest
            {
                Keyword = keyword,
                IsActive = isActive,
                Gender = gender,
                PageNumber = pageNumber,
                PageSize = pageSize,
                SortBy = sortBy,
                SortDescending = sortDescending
            };

            var result = await _employeeService.GetPagedListAsync(filter);
            return Ok(result);
        }

        [HttpPost]
        [ProducesResponseType(typeof(ApiResult<EmployeeListDto>), StatusCodes.Status201Created)]
        public async Task<IActionResult> Create([FromBody] CreateEmployeeRequest request)
        {
            var result = await _employeeService.CreateAsync(request);
            if (!result.Success)
                return BadRequest(result);

            return CreatedAtAction(nameof(GetList), result);
        }

        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(ApiResult<EmployeeListDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEmployeeRequest request)
        {
            var result = await _employeeService.UpdateAsync(id, request);
            if (!result.Success)
                return result.Message.Contains("Không tìm thấy") ? NotFound(result) : BadRequest(result);

            return Ok(result);
        }

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Deactivate(Guid id, [FromQuery] string? lockReason = null)
        {
            var result = await _employeeService.DeactivateAsync(id, lockReason);
            if (!result.Success)
                return result.Message.Contains("Không tìm thấy") ? NotFound(result) : BadRequest(result);

            return Ok(result);
        }

        [HttpPut("{id:guid}/activate")]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Reactivate(Guid id)
        {
            var result = await _employeeService.ReactivateAsync(id);
            if (!result.Success)
                return result.Message.Contains("Không tìm thấy") ? NotFound(result) : BadRequest(result);

            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ApiResult<EmployeeListDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _employeeService.GetByIdAsync(id);
            if (!result.Success) return NotFound(result);
            return Ok(result);
        }

        [AllowAnonymous]
        [HttpGet("technicians")]
        [ProducesResponseType(typeof(ApiResult<List<EmployeeDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTechnicians()
        {
            var result = await _employeeService.GetTechniciansSimpleAsync();
            return Ok(result);
        }
    }
}
