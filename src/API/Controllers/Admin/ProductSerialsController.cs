using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBL3.Application.ProductSerials;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Inventory;
using PBL3.API.Extensions;

namespace PBL3.API.Controllers.Admin
{
    [ApiController]
    [Route("api/product-serials")]
    [Produces("application/json")]
    [Authorize(Roles = "Admin, Employee")]
    public class ProductSerialsController : ControllerBase
    {
        private readonly IProductSerialService _productSerialService;

        public ProductSerialsController(IProductSerialService productSerialService)
        {
            _productSerialService = productSerialService;
        }

        /// <summary>
        /// Kiểm tra mã Serial đã tồn tại trong DB chưa (Real-time check khi quét mã vạch).
        /// </summary>
        [HttpGet("check-exist")]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CheckExist(
            [FromQuery] string serialNumber,
            [FromQuery] int variantId)
        {
            if (string.IsNullOrWhiteSpace(serialNumber))
                return BadRequest(ApiResult<bool>.Fail("Mã Serial không được để trống."));

            if (variantId <= 0)
                return BadRequest(ApiResult<bool>.Fail("VariantId không hợp lệ."));

            var result = await _productSerialService.CheckExistAsync(serialNumber.Trim(), variantId);
            return Ok(result);
        }

        /// <summary>
        /// Thống kê số lượng Serial theo trạng thái. Có thể lọc theo productId hoặc variantId.
        /// </summary>
        [HttpGet("statistics")]
        [ProducesResponseType(typeof(ApiResult<ProductSerialStatisticsDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetStatistics(
            [FromQuery] int? productId,
            [FromQuery] int? variantId)
        {
            var result = await _productSerialService.GetStatisticsAsync(productId, variantId);
            return Ok(result);
        }

        /// <summary>
        /// Danh sách phân trang Serial với bộ lọc đa điều kiện.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResult<PagedResult<ProductSerialListDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetList([FromQuery] ProductSerialFilterRequest filter)
        {
            var result = await _productSerialService.GetPagedListAsync(filter);
            return Ok(result);
        }

        /// <summary>
        /// Lấy chi tiết một Serial kèm thông tin variant, phiếu nhập và đơn hàng (nếu đã bán).
        /// </summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ApiResult<ProductSerialDetailDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<ProductSerialDetailDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _productSerialService.GetByIdAsync(id);
            return result.ToActionResult(this);
        }

        /// <summary>
        /// Thay đổi trạng thái Serial: đánh dấu lỗi (3), hoàn trả (4), hoặc khôi phục về kho (0).
        /// </summary>
        [HttpPatch("{id:int}/status")]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateSerialStatusRequest request)
        {
            if (id <= 0)
                return BadRequest(ApiResult<bool>.Fail("Id Serial không hợp lệ."));

            var result = await _productSerialService.UpdateStatusAsync(id, request);
            return result.ToActionResult(this);
        }
    }
}
