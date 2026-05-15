using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Customers;
using System.Security.Claims;

namespace PBL3.API.Controllers.Storefront
{
    [ApiController]
    [Route("api/storefront/profile")]
    [Produces("application/json")]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;

        public ProfileController(UserManager<AppUser> userManager)
        {
            _userManager = userManager;
        }

        [HttpGet("me")]
        [ProducesResponseType(typeof(ApiResult<CustomerDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyProfile()
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized(ApiResult<CustomerDto>.Fail("Người dùng chưa đăng nhập."));

            var user = await _userManager.Users
                .Include(u => u.Profile)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return NotFound(ApiResult<CustomerDto>.Fail("Không tìm thấy thông tin người dùng."));

            return Ok(ApiResult<CustomerDto>.Ok(MapToDto(user)));
        }

        [HttpPut("me")]
        [ProducesResponseType(typeof(ApiResult<CustomerDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<CustomerDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateCustomerRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized(ApiResult<CustomerDto>.Fail("Người dùng chưa đăng nhập."));

            var user = await _userManager.Users
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return NotFound(ApiResult<CustomerDto>.Fail("Không tìm thấy thông tin người dùng."));

            if (user.Profile == null)
                user.Profile = new UserProfile { UserId = userId };

            user.Profile.FullName = request.FullName.Trim();
            user.Profile.Gender = request.Gender;
            user.Profile.DateOfBirth = request.DateOfBirth;
            user.Profile.AvatarUrl = request.AvatarUrl;
            user.Profile.Address = request.Address?.Trim();
            user.Profile.City = request.City?.Trim();

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return BadRequest(ApiResult<CustomerDto>.Fail("Cập nhật thất bại. " + errors));
            }

            return Ok(ApiResult<CustomerDto>.Ok(MapToDto(user), "Cập nhật thông tin thành công."));
        }

        private Guid GetCurrentUserId()
        {
            var str = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(str, out var id) ? id : Guid.Empty;
        }

        private static CustomerDto MapToDto(AppUser user) => new()
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber ?? string.Empty,
            FullName = user.Profile?.FullName ?? string.Empty,
            Gender = user.Profile?.Gender ?? 0,
            DateOfBirth = user.Profile?.DateOfBirth,
            AvatarUrl = user.Profile?.AvatarUrl,
            Address = user.Profile?.Address,
            City = user.Profile?.City,
            IsActive = user.IsActive,
            CreatedDate = user.CreatedDate
        };
    }
}
