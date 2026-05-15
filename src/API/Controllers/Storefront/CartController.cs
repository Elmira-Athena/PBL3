using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBL3.Service.Cart;
using PBL3.Shared.DTOs.Cart;
using PBL3.Shared.DTOs.Common;

namespace PBL3.API.Controllers.Storefront
{
    [ApiController]
    [Route("api/cart")]
    [Authorize]
    public class CartController : ControllerBase
    {
        private readonly ICartService _cartService;

        public CartController(ICartService cartService)
        {
            _cartService = cartService;
        }

        private Guid GetUserId()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            {
                throw new UnauthorizedAccessException("Không thể xác định danh tính người dùng.");
            }
            return userId;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ApiResult<CartResponse>), 200)]
        public async Task<IActionResult> GetMyCart()
        {
            try
            {
                var userId = GetUserId();
                var result = await _cartService.GetMyCartAsync(userId);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ApiResult<CartResponse>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResult<CartResponse>.Fail(ex.Message));
            }
        }

        [HttpPost]
        [ProducesResponseType(typeof(ApiResult<CartResponse>), 200)]
        public async Task<IActionResult> AddToCart([FromBody] AddToCartRequest request)
        {
            try
            {
                var userId = GetUserId();
                var result = await _cartService.AddToCartAsync(userId, request);
                if (!result.Success) return BadRequest(result);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ApiResult<CartResponse>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResult<CartResponse>.Fail(ex.Message));
            }
        }

        [HttpPut("items/{id}")]
        [ProducesResponseType(typeof(ApiResult<CartResponse>), 200)]
        public async Task<IActionResult> UpdateQuantity(int id, [FromBody] UpdateCartItemRequest request)
        {
            try
            {
                var userId = GetUserId();
                var result = await _cartService.UpdateQuantityAsync(userId, id, request);
                if (!result.Success) return BadRequest(result);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ApiResult<CartResponse>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResult<CartResponse>.Fail(ex.Message));
            }
        }

        [HttpDelete("items/{id}")]
        [ProducesResponseType(typeof(ApiResult<CartResponse>), 200)]
        public async Task<IActionResult> RemoveItem(int id)
        {
            try
            {
                var userId = GetUserId();
                var result = await _cartService.RemoveItemAsync(userId, id);
                if (!result.Success) return BadRequest(result);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ApiResult<CartResponse>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResult<CartResponse>.Fail(ex.Message));
            }
        }
    }
}
