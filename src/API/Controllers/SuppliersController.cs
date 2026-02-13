using Microsoft.AspNetCore.Mvc;
using PBL3.Service.Suppliers;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Products;
using PBL3.Shared.DTOs.Suppliers;

namespace PBL3.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class SuppliersController : ControllerBase
    {
        private readonly ISupplierService _supplierService;

        public SuppliersController(ISupplierService supplierService)
        {
            _supplierService = supplierService;
        }

        /// <summary>
        /// Lấy danh sách nhà cung cấp (phân trang, tìm kiếm theo Tên hoặc SĐT).
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResult<PagedResult<SupplierDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetList([FromQuery] SupplierFilterRequest filter)
        {
            var result = await _supplierService.GetPagedListAsync(filter);
            return Ok(result);
        }

        /// <summary>
        /// Lấy chi tiết nhà cung cấp theo Id.
        /// </summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ApiResult<SupplierDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<SupplierDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _supplierService.GetByIdAsync(id);

            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }

        /// <summary>
        /// Tạo mới nhà cung cấp.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResult<SupplierDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResult<SupplierDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CreateSupplierRequest request)
        {
            var result = await _supplierService.CreateAsync(request);

            if (!result.Success)
                return BadRequest(result);

            return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
        }

        /// <summary>
        /// Cập nhật nhà cung cấp.
        /// </summary>
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(ApiResult<SupplierDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<SupplierDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResult<SupplierDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateSupplierRequest request)
        {
            var result = await _supplierService.UpdateAsync(id, request);

            if (!result.Success)
            {
                if (result.Message.Contains("Không tìm thấy"))
                    return NotFound(result);

                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Xóa mềm nhà cung cấp (Soft Delete).
        /// </summary>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _supplierService.DeleteAsync(id);

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
