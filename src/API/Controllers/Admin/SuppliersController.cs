using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBL3.Application.Suppliers;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Products;
using PBL3.Shared.DTOs.Suppliers;
using PBL3.API.Extensions;

namespace PBL3.API.Controllers.Admin
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize(Roles = "Admin, Employee")]
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
            return result.ToActionResult(this);
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

            if (!result.Success) return result.ToActionResult(this);

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
            return result.ToActionResult(this);
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
            return result.ToActionResult(this);
        }
    }
}
