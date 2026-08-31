using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBL3.Service.Cart;
using PBL3.Shared.DTOs.Cart;
using PBL3.Core.Exceptions;
using PBL3.Shared.DTOs.Common;

namespace PBL3.API.Controllers.Storefront
{
    [ApiController]
    [Route("api/cart")]
    [Authorize]
    public class CartController : ControllerBase
    {
        private readonly ICartService _cartService;
        private readonly ILogger<CartController> _logger;

        public CartController(ICartService cartService, ILogger<CartController> logger)
        {
            _cartService = cartService;
            _logger = logger;
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
            catch (BusinessRuleException ex)
            {
                // Thông báo nghiệp vụ đã soạn cho người dùng — trả NGUYÊN VĂN.
                return BadRequest(ApiResult<CartResponse>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                // KHÔNG relay ex.Message: lỗi hạ tầng (EF Core / SQL Server) là tiếng Anh và
                // lộ nội tạng ORM. CartService không ném exception nghiệp vụ nào, nên trước
                // khi sửa thì khối này CHỈ có thể rò rỉ lỗi hạ tầng. Xem mục 🅷 của runbook.
                _logger.LogError(ex, "Lỗi khi đọc giỏ hàng.");
                return BadRequest(ApiResult<CartResponse>.Fail(
                    "Không thể tải giỏ hàng do lỗi hệ thống. Vui lòng tải lại trang."));
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
            catch (BusinessRuleException ex)
            {
                // Thông báo nghiệp vụ đã soạn cho người dùng — trả NGUYÊN VĂN.
                return BadRequest(ApiResult<CartResponse>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                // KHÔNG relay ex.Message: lỗi hạ tầng (EF Core / SQL Server) là tiếng Anh và
                // lộ nội tạng ORM. CartService không ném exception nghiệp vụ nào, nên trước
                // khi sửa thì khối này CHỈ có thể rò rỉ lỗi hạ tầng. Xem mục 🅷 của runbook.
                _logger.LogError(ex, "Lỗi khi thêm sản phẩm vào giỏ hàng.");
                return BadRequest(ApiResult<CartResponse>.Fail(
                    "Không thể thêm sản phẩm vào giỏ do lỗi hệ thống. Vui lòng thử lại."));
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
            catch (BusinessRuleException ex)
            {
                // Thông báo nghiệp vụ đã soạn cho người dùng — trả NGUYÊN VĂN.
                return BadRequest(ApiResult<CartResponse>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                // KHÔNG relay ex.Message: lỗi hạ tầng (EF Core / SQL Server) là tiếng Anh và
                // lộ nội tạng ORM. CartService không ném exception nghiệp vụ nào, nên trước
                // khi sửa thì khối này CHỈ có thể rò rỉ lỗi hạ tầng. Xem mục 🅷 của runbook.
                _logger.LogError(ex, "Lỗi khi cập nhật số lượng trong giỏ hàng.");
                return BadRequest(ApiResult<CartResponse>.Fail(
                    "Không thể cập nhật số lượng do lỗi hệ thống. Vui lòng thử lại."));
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
            catch (BusinessRuleException ex)
            {
                // Thông báo nghiệp vụ đã soạn cho người dùng — trả NGUYÊN VĂN.
                return BadRequest(ApiResult<CartResponse>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                // KHÔNG relay ex.Message: lỗi hạ tầng (EF Core / SQL Server) là tiếng Anh và
                // lộ nội tạng ORM. CartService không ném exception nghiệp vụ nào, nên trước
                // khi sửa thì khối này CHỈ có thể rò rỉ lỗi hạ tầng. Xem mục 🅷 của runbook.
                _logger.LogError(ex, "Lỗi khi xoá sản phẩm khỏi giỏ hàng.");
                return BadRequest(ApiResult<CartResponse>.Fail(
                    "Không thể xoá sản phẩm khỏi giỏ do lỗi hệ thống. Vui lòng thử lại."));
            }
        }
    }
}
