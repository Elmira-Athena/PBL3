using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBL3.Service.Customers;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Customers;
using System.Security.Claims;

namespace PBL3.API.Controllers.Storefront
{
    [ApiController]
    [Route("api/storefront/profile")]
    [Produces("application/json")]
    [Authorize(Roles = "Customer")]
    public class ProfileController : ControllerBase
    {
        private readonly ICustomerService _customerService;

        public ProfileController(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        [HttpGet("me")]
        [ProducesResponseType(typeof(ApiResult<CustomerDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyProfile()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(ApiResult<CustomerDto>.Fail("Người dùng chưa đăng nhập."));

            var result = await _customerService.GetByIdAsync(userId);
            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }

        [HttpPut("me")]
        [ProducesResponseType(typeof(ApiResult<CustomerDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<CustomerDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateCustomerRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(ApiResult<CustomerDto>.Fail("Người dùng chưa đăng nhập."));

            var result = await _customerService.UpdateAsync(userId, request);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }
    }
}
