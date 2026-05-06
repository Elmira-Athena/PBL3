using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBL3.Service.Vouchers;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Vouchers;

namespace PBL3.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class VouchersController : ControllerBase
    {
        private readonly IVoucherService _voucherService;

        public VouchersController(IVoucherService voucherService)
        {
            _voucherService = voucherService;
        }

        /// <summary>
        /// Lấy danh sách voucher có phân trang, hỗ trợ tìm kiếm theo Code/Tên, lọc trạng thái và khoảng thời gian.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin, Employee")]
        [ProducesResponseType(typeof(ApiResult<PagedResult<VoucherDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetList([FromQuery] VoucherFilterRequest filter)
        {
            var result = await _voucherService.GetPagedListAsync(filter);
            return Ok(result);
        }

        /// <summary>
        /// Lấy chi tiết voucher theo Id.
        /// </summary>
        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin, Employee")]
        [ProducesResponseType(typeof(ApiResult<VoucherDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<VoucherDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _voucherService.GetByIdAsync(id);

            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }

        /// <summary>
        /// Tạo mới voucher. Chỉ Admin được phép.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ApiResult<VoucherDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResult<VoucherDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CreateVoucherRequest request)
        {
            var result = await _voucherService.CreateAsync(request);

            if (!result.Success)
                return BadRequest(result);

            return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
        }

        /// <summary>
        /// Cập nhật voucher. Chỉ Admin được phép. Mã Code không thể thay đổi.
        /// </summary>
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ApiResult<VoucherDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<VoucherDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResult<VoucherDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateVoucherRequest request)
        {
            var result = await _voucherService.UpdateAsync(id, request);

            if (!result.Success)
            {
                if (result.Message.Contains("Không tìm thấy"))
                    return NotFound(result);

                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Xóa mềm voucher. Chỉ Admin được phép.
        /// </summary>
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _voucherService.DeleteAsync(id);

            if (!result.Success)
            {
                if (result.Message.Contains("Không tìm thấy"))
                    return NotFound(result);

                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Bật/tắt trạng thái hoạt động của voucher. Chỉ Admin được phép.
        /// </summary>
        [HttpPatch("{id:int}/toggle-status")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ApiResult<VoucherDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<VoucherDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var result = await _voucherService.ToggleStatusAsync(id);

            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }

        /// <summary>
        /// Kiểm tra tính hợp lệ của mã voucher và preview số tiền giảm trước khi đặt hàng.
        /// Không yêu cầu đăng nhập, nhưng nếu đã đăng nhập sẽ kiểm tra MaxUsesPerUser.
        /// </summary>
        [HttpPost("validate-code")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResult<ValidateVoucherResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ValidateCode([FromBody] ValidateVoucherRequest request)
        {
            Guid? userId = null;
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var parsedId))
                userId = parsedId;

            var result = await _voucherService.ValidateVoucherCodeAsync(request, userId);
            return Ok(result);
        }
    }
}
