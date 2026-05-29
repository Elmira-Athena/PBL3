using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBL3.Application.Banners;
using PBL3.Shared.DTOs.Banners;
using PBL3.Shared.DTOs.Common;
using PBL3.API.Extensions;

namespace PBL3.API.Controllers.Admin
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
            return result.ToActionResult(this);
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
            if (!result.Success) return result.ToActionResult(this);

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
            return result.ToActionResult(this);
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
            return result.ToActionResult(this);
        }
    }
}
