using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBL3.Application.Products;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Products;
using PBL3.API.Extensions;

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
            return result.ToActionResult(this);
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

            if (!result.Success) return result.ToActionResult(this);

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
            return result.ToActionResult(this);
        }

        /// <summary>
        /// Thêm phiên bản (Variant) mới cho sản phẩm đã tồn tại.
        /// </summary>
        [HttpPost("{id:int}/variants")]
        [ProducesResponseType(typeof(ApiResult<ProductVariantDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResult<ProductVariantDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResult<ProductVariantDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AddVariant(int id, [FromBody] SaveVariantRequest request)
        {
            var result = await _productService.AddVariantAsync(id, request);

            if (!result.Success) return result.ToActionResult(this);

            return CreatedAtAction(nameof(GetById), new { id }, result);
        }

        /// <summary>
        /// Cập nhật danh sách ảnh cho sản phẩm (thay toàn bộ ảnh của tất cả variants).
        /// </summary>
        [HttpPut("{id:int}/images")]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateImages(int id, [FromBody] List<SaveImageRequest> images)
        {
            var result = await _productService.UpdateImagesAsync(id, images);
            return result.ToActionResult(this);
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
            return result.ToActionResult(this);
        }
    }
}
