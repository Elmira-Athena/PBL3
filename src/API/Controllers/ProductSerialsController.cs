using Microsoft.AspNetCore.Mvc;
using PBL3.Service.ProductSerials;
using PBL3.Shared.DTOs.Common;

namespace PBL3.API.Controllers
{
    [ApiController]
    [Route("api/product-serials")]
    [Produces("application/json")]
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
        /// <param name="serialNumber">Mã Serial cần kiểm tra.</param>
        /// <param name="variantId">Id của ProductVariant.</param>
        /// <returns>true nếu đã tồn tại, false nếu mã mới.</returns>
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
    }
}
