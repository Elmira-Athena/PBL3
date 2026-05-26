using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBL3.Service.Products;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Products;

namespace PBL3.API.Controllers.Admin
{
    /// <summary>
    /// Quản lý ProductVariant ở cấp độ độc lập: metadata, ảnh, thông số kỹ thuật.
    /// Việc tạo Product mới với nested variants vẫn dùng <see cref="ProductsController.Create" />.
    /// </summary>
    [ApiController]
    [Route("api/products")]
    [Produces("application/json")]
    [Authorize(Roles = "Admin, Employee")]
    public class ProductVariantsController : ControllerBase
    {
        private readonly IProductVariantService _variantService;

        public ProductVariantsController(IProductVariantService variantService)
        {
            _variantService = variantService;
        }

        /// <summary>
        /// Lấy chi tiết 1 variant kèm ảnh + specs.
        /// </summary>
        [HttpGet("variants/{variantId:int}")]
        [ProducesResponseType(typeof(ApiResult<ProductVariantDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<ProductVariantDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int variantId)
        {
            var result = await _variantService.GetByIdAsync(variantId);
            return result.Success ? Ok(result) : NotFound(result);
        }

        /// <summary>
        /// Thêm variant mới vào product đã tồn tại.
        /// </summary>
        [HttpPost("{productId:int}/variants")]
        [ProducesResponseType(typeof(ApiResult<ProductVariantDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResult<ProductVariantDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResult<ProductVariantDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Create(int productId, [FromBody] SaveVariantRequest request)
        {
            var result = await _variantService.CreateAsync(productId, request);
            if (!result.Success)
                return result.Message.Contains("Không tìm thấy") ? NotFound(result) : BadRequest(result);
            return CreatedAtAction(nameof(GetById), new { variantId = result.Data!.Id }, result);
        }

        /// <summary>
        /// Cập nhật metadata (SKU/tên/giá/bảo hành) của 1 variant.
        /// </summary>
        [HttpPut("variants/{variantId:int}")]
        [ProducesResponseType(typeof(ApiResult<ProductVariantDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<ProductVariantDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResult<ProductVariantDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(int variantId, [FromBody] UpdateVariantRequest request)
        {
            var result = await _variantService.UpdateAsync(variantId, request);
            if (!result.Success)
                return result.Message.Contains("Không tìm thấy") ? NotFound(result) : BadRequest(result);
            return Ok(result);
        }

        /// <summary>
        /// Thay toàn bộ ảnh của 1 variant.
        /// </summary>
        [HttpPut("variants/{variantId:int}/images")]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ReplaceImages(int variantId, [FromBody] SaveVariantImagesRequest request)
        {
            var result = await _variantService.ReplaceImagesAsync(variantId, request);
            if (!result.Success)
                return result.Message.Contains("Không tìm thấy") ? NotFound(result) : BadRequest(result);
            return Ok(result);
        }

        /// <summary>
        /// Thay toàn bộ thông số kỹ thuật của 1 variant.
        /// </summary>
        [HttpPut("variants/{variantId:int}/specifications")]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ReplaceSpecifications(int variantId, [FromBody] SaveVariantSpecificationsRequest request)
        {
            var result = await _variantService.ReplaceSpecificationsAsync(variantId, request);
            if (!result.Success)
                return result.Message.Contains("Không tìm thấy") ? NotFound(result) : BadRequest(result);
            return Ok(result);
        }

        /// <summary>
        /// Soft delete 1 variant. Bị chặn nếu là variant duy nhất còn lại của product.
        /// </summary>
        [HttpDelete("variants/{variantId:int}")]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int variantId)
        {
            var result = await _variantService.DeleteAsync(variantId);
            if (!result.Success)
                return result.Message.Contains("Không tìm thấy") ? NotFound(result) : BadRequest(result);
            return Ok(result);
        }
    }
}
