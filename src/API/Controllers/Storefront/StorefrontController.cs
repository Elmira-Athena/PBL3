using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBL3.Application.Storefront;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Storefront;
using PBL3.API.Extensions;

namespace PBL3.API.Controllers.Storefront
{
    [ApiController]
    [Route("api/storefront")]
    [Produces("application/json")]
    [AllowAnonymous]
    public class StorefrontController : ControllerBase
    {
        private readonly IStorefrontService _storefrontService;

        public StorefrontController(IStorefrontService storefrontService)
        {
            _storefrontService = storefrontService;
        }

        /// <summary>
        /// Lấy danh sách danh mục đang hoạt động cho Menu.
        /// </summary>
        [HttpGet("categories")]
        [ProducesResponseType(typeof(ApiResult<List<CategoryMenuResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCategories()
        {
            var result = await _storefrontService.GetActiveCategoriesAsync();
            return Ok(result);
        }

        /// <summary>
        /// Lấy danh sách sản phẩm nổi bật.
        /// </summary>
        [HttpGet("products/featured")]
        [ProducesResponseType(typeof(ApiResult<List<ProductCardResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetFeaturedProducts([FromQuery] int? categoryId, [FromQuery] int take = 5)
        {
            var result = await _storefrontService.GetFeaturedProductsAsync(categoryId, take);
            return Ok(result);
        }
        /// <summary>
        /// Lấy thông tin chi tiết sản phẩm.
        /// </summary>
        [HttpGet("products/{slug}")]
        [ProducesResponseType(typeof(ApiResult<ProductDetailResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetProductDetail(string slug)
        {
            var result = await _storefrontService.GetProductDetailAsync(slug);
            return result.ToActionResult(this);
        }

        /// <summary>
        /// Lấy danh sách sản phẩm liên quan.
        /// </summary>
        [HttpGet("products/{slug}/related")]
        [ProducesResponseType(typeof(ApiResult<List<ProductCardResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetRelatedProducts(string slug)
        {
            var result = await _storefrontService.GetRelatedProductsAsync(slug);
            return Ok(result);
        }

        /// <summary>
        /// Lấy thông tin danh mục theo slug (dùng cho breadcrumb và tiêu đề trang).
        /// </summary>
        [HttpGet("categories/{slug}")]
        [ProducesResponseType(typeof(ApiResult<CategoryDetailResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCategoryBySlug(string slug)
        {
            var result = await _storefrontService.GetCategoryBySlugAsync(slug);
            return result.ToActionResult(this);
        }

        /// <summary>
        /// Tìm kiếm sản phẩm theo từ khóa, danh mục và khoảng giá với phân trang.
        /// </summary>
        [HttpGet("products/search")]
        [ProducesResponseType(typeof(ApiResult<PagedResult<ProductCardResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> SearchProducts(
            [FromQuery] string? keyword,
            [FromQuery] int? categoryId,
            [FromQuery] decimal? priceMin,
            [FromQuery] decimal? priceMax,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _storefrontService.SearchProductsAsync(keyword, categoryId, priceMin, priceMax, page, pageSize);
            return Ok(result);
        }

        /// <summary>
        /// Lấy danh sách sản phẩm theo danh mục (bao gồm cả danh mục con) với phân trang.
        /// </summary>
        [HttpGet("categories/{slug}/products")]
        [ProducesResponseType(typeof(ApiResult<PagedResult<ProductCardResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetProductsByCategory(
            string slug,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _storefrontService.GetProductsByCategoryAsync(slug, page, pageSize);
            return result.ToActionResult(this);
        }
    }
}
