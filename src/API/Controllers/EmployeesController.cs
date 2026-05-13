using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PBL3.Core.Entities;
using PBL3.Shared.DTOs.Common;

namespace PBL3.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin, Employee")]
    public class EmployeesController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;

        public EmployeesController(UserManager<AppUser> userManager)
        {
            _userManager = userManager;
        }

        /// <summary>
        /// Lấy danh sách tất cả nhân viên (Admin, Employee)
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResult<List<EmployeeDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetEmployees()
        {
            try
            {
                var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
                var employeeUsers = await _userManager.GetUsersInRoleAsync("Employee");

                var employees = new List<EmployeeDto>();

                foreach (var user in adminUsers.Union(employeeUsers))
                {
                    employees.Add(new EmployeeDto
                    {
                        Id = user.Id,
                        FullName = user.Profile?.FullName ?? user.UserName ?? "N/A"
                    });
                }

                return Ok(ApiResult<List<EmployeeDto>>.Ok(employees.OrderBy(e => e.FullName).ToList()));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResult<List<EmployeeDto>>.Fail($"Lỗi: {ex.Message}"));
            }
        }
    }
}
