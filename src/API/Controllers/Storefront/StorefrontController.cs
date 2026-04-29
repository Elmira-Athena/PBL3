using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBL3.Service.Storefront;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Storefront;

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
    }
}
