using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBL3.Service.Products;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Products;

namespace PBL3.API.Controllers.Admin
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize(Roles = "Admin, Employee")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductsController(IProductService productService)
        {
            _productService = productService;
        }

        /// <summary>
        /// Lấy danh sách sản phẩm (phân trang, lọc theo Category/Price/Keyword).
        /// </summary>
        [AllowAnonymous]
        [HttpGet]
        [ProducesResponseType(typeof(ApiResult<PagedResult<ProductListDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetList([FromQuery] ProductFilterRequest request)
        {
            var result = await _productService.GetListAsync(request);
            return Ok(result);
        }

        /// <summary>
        /// Lấy chi tiết sản phẩm theo Id (bao gồm Variants, Images, Attributes).
        /// </summary>
        [AllowAnonymous]
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ApiResult<ProductDetailDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<ProductDetailDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _productService.GetByIdAsync(id);

            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }

        /// <summary>
        /// Tạo mới sản phẩm trọn gói (Product + Variants).
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResult<ProductDetailDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResult<ProductDetailDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CreateProductRequest request)
        {
            var result = await _productService.CreateAsync(request);

            if (!result.Success)
                return BadRequest(result);

            return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
        }

        /// <summary>
        /// Cập nhật thông tin chung sản phẩm.
        /// </summary>
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(ApiResult<ProductDetailDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<ProductDetailDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResult<ProductDetailDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateProductRequest request)
        {
            var result = await _productService.UpdateAsync(id, request);

            if (!result.Success)
            {
                if (result.Message.Contains("Không tìm thấy"))
                    return NotFound(result);

                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Xóa mềm sản phẩm (Soft Delete).
        /// </summary>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _productService.DeleteAsync(id);

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
