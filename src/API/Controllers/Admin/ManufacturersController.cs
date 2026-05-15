using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBL3.Service.Manufacturers;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Manufacturers;

namespace PBL3.API.Controllers.Admin
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ManufacturersController : ControllerBase
    {
        private readonly IManufacturerService _manufacturerService;

        public ManufacturersController(IManufacturerService manufacturerService)
        {
            _manufacturerService = manufacturerService;
        }

        /// <summary>
        /// Lấy danh sách hãng sản xuất (phân trang, tìm kiếm theo Tên hoặc Website).
        /// Cho phép cả 3 role truy cập (dùng để xem/filter).
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin, Employee")]
        [ProducesResponseType(typeof(ApiResult<PagedResult<ManufacturerDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetList([FromQuery] ManufacturerFilterRequest filter)
        {
            var result = await _manufacturerService.GetPagedListAsync(filter);
            return Ok(result);
        }

        /// <summary>
        /// Lấy danh sách hãng rút gọn (Id + Name + Logo) dùng cho Dropdown.
        /// Endpoint chuyên biệt để form "Thêm sản phẩm" load danh sách hãng.
        /// </summary>
        [HttpGet("dropdown")]
        [Authorize(Roles = "Admin, Employee")]
        [ProducesResponseType(typeof(ApiResult<List<ManufacturerSummaryDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDropdown()
        {
            var result = await _manufacturerService.GetAllForDropdownAsync();
            return Ok(result);
        }

        /// <summary>
        /// Lấy chi tiết hãng sản xuất theo Id.
        /// </summary>
        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin, Employee")]
        [ProducesResponseType(typeof(ApiResult<ManufacturerDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<ManufacturerDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _manufacturerService.GetByIdAsync(id);

            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }

        /// <summary>
        /// Tạo mới hãng sản xuất. Chỉ Admin được phép.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ApiResult<ManufacturerDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResult<ManufacturerDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CreateManufacturerRequest request)
        {
            var result = await _manufacturerService.CreateAsync(request);

            if (!result.Success)
                return BadRequest(result);

            return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
        }

        /// <summary>
        /// Cập nhật hãng sản xuất. Chỉ Admin được phép.
        /// </summary>
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ApiResult<ManufacturerDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<ManufacturerDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResult<ManufacturerDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateManufacturerRequest request)
        {
            var result = await _manufacturerService.UpdateAsync(id, request);

            if (!result.Success)
            {
                if (result.Message.Contains("Không tìm thấy"))
                    return NotFound(result);

                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Xóa mềm hãng sản xuất. Chỉ Admin được phép.
        /// Sẽ thất bại nếu hãng còn sản phẩm đang hoạt động.
        /// </summary>
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _manufacturerService.DeleteAsync(id);

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
