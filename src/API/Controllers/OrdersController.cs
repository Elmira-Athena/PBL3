using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBL3.Service.Orders;
using PBL3.Shared.DTOs.Sale;
using PBL3.Shared.DTOs.Common;

namespace PBL3.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public OrdersController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        [HttpPost("checkout")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request)
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized(ApiResult<CheckoutResponse>.Fail("Không thể xác thực thông tin người dùng."));
            }

            try
            {
                var result = await _orderService.CheckoutAsync(request, userId);
                if (!result.Success) return BadRequest(result);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResult<CheckoutResponse>.Fail(ex.Message));
            }
        }

        [HttpGet("my")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> GetMyOrders([FromQuery] OrderFilterRequest request)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(ApiResult<PagedResult<OrderSummaryResponse>>.Fail("Không thể xác thực thông tin người dùng."));
            var result = await _orderService.GetMyOrdersAsync(userId, request);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _orderService.GetByIdAsync(id);
            if (!result.Success)
            {
                return NotFound(result);
            }
            return Ok(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetPagedOrders([FromQuery] OrderFilterRequest request)
        {
            var result = await _orderService.GetPagedOrdersAsync(request);
            if (!result.Success)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }

        [HttpPut("{id}/cancel")]
        [Authorize(Roles = "Admin, Employee")]
        public async Task<IActionResult> CancelOrder(int id, [FromBody] CancelOrderRequest request)
        {
            try
            {
                var result = await _orderService.CancelOrderAsync(id, request);
                if (!result.Success)
                {
                    return BadRequest(result);
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResult<bool>.Fail(ex.Message));
            }
        }

        [HttpPut("{id}/complete")]
        [Authorize(Roles = "Admin, Employee")]
        public async Task<IActionResult> CompleteOrder(int id)
        {
            try
            {
                var result = await _orderService.CompleteOrderAsync(id);
                if (!result.Success)
                {
                    return BadRequest(result);
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResult<bool>.Fail(ex.Message));
            }
        }

        [HttpPut("{id}/confirm")]
        [Authorize(Roles = "Admin, Employee")]
        public async Task<IActionResult> ConfirmOrder(int id)
        {
            try
            {
                var result = await _orderService.ConfirmOrderAsync(id);
                if (!result.Success)
                    return BadRequest(result);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResult<bool>.Fail(ex.Message));
            }
        }
    }
}
