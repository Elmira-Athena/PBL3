using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBL3.Service.Banners;
using PBL3.Shared.DTOs.Banners;
using PBL3.Shared.DTOs.Common;

namespace PBL3.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class BannersController : ControllerBase
    {
        private readonly IBannerService _bannerService;

        public BannersController(IBannerService bannerService)
        {
            _bannerService = bannerService;
        }

        /// <summary>
        /// Lấy danh sách banner (phân trang, tìm kiếm theo tiêu đề). Chỉ Admin.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ApiResult<PagedResult<BannerDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetList([FromQuery] BannerFilterRequest filter)
        {
            var result = await _bannerService.GetPagedListAsync(filter);
            return Ok(result);
        }

        /// <summary>
        /// Lấy danh sách banner đang hiệu lực. Public — dùng cho trang chủ.
        /// </summary>
        [HttpGet("active")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResult<List<BannerPublicDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetActive()
        {
            var result = await _bannerService.GetActiveAsync();
            return Ok(result);
        }

        /// <summary>
        /// Lấy chi tiết banner theo Id. Chỉ Admin.
        /// </summary>
        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ApiResult<BannerDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<BannerDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _bannerService.GetByIdAsync(id);
            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }

        /// <summary>
        /// Tạo mới banner. Chỉ Admin.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ApiResult<BannerDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResult<BannerDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CreateBannerRequest request)
        {
            var result = await _bannerService.CreateAsync(request);
            if (!result.Success)
                return BadRequest(result);

            return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
        }

        /// <summary>
        /// Cập nhật banner. Chỉ Admin.
        /// </summary>
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ApiResult<BannerDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<BannerDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResult<BannerDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateBannerRequest request)
        {
            var result = await _bannerService.UpdateAsync(id, request);
            if (!result.Success)
            {
                if (result.Message.Contains("Không tìm thấy"))
                    return NotFound(result);

                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Xóa mềm banner. Chỉ Admin.
        /// </summary>
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _bannerService.DeleteAsync(id);
            if (!result.Success)
            {
                if (result.Message.Contains("Không tìm thấy"))
                    return NotFound(result);

                return BadRequest(result);
            }

            return Ok(result);
        }
    }
}
