using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBL3.Application.Orders;
using PBL3.Shared.DTOs.Sale;
using PBL3.Shared.DTOs.Common;
using PBL3.API.Extensions;

namespace PBL3.API.Controllers.Admin
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
                return result.ToActionResult(this);
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
            return result.ToActionResult(this);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            if (User.IsInRole("Customer"))
            {
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdStr, out var userId))
                    return Unauthorized(ApiResult<OrderDetailDto>.Fail("Không thể xác thực thông tin người dùng."));

                var myResult = await _orderService.GetMyOrderByIdAsync(id, userId);
                return myResult.ToActionResult(this);
            }

            var result = await _orderService.GetByIdAsync(id);
            return result.ToActionResult(this);
        }

        [HttpPut("my/{id}/cancel")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> CancelMyOrder(int id, [FromBody] CancelOrderRequest request)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(ApiResult<bool>.Fail("Không thể xác thực thông tin người dùng."));

            try
            {
                var result = await _orderService.CancelMyOrderAsync(id, userId, request?.CancelReason ?? string.Empty);
                return result.ToActionResult(this);
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResult<bool>.Fail(ex.Message));
            }
        }

        [HttpPut("my/{id}/confirm-received")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> ConfirmReceived(int id)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(ApiResult<bool>.Fail("Không thể xác thực thông tin người dùng."));

            try
            {
                var result = await _orderService.ConfirmReceivedByCustomerAsync(id, userId);
                return result.ToActionResult(this);
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResult<bool>.Fail(ex.Message));
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin, Employee")]
        public async Task<IActionResult> GetPagedOrders([FromQuery] OrderFilterRequest request)
        {
            var result = await _orderService.GetPagedOrdersAsync(request);
            return result.ToActionResult(this);
        }

        [HttpPut("{id}/cancel")]
        [Authorize(Roles = "Admin, Employee")]
        public async Task<IActionResult> CancelOrder(int id, [FromBody] CancelOrderRequest request)
        {
            try
            {
                var result = await _orderService.CancelOrderAsync(id, request);
                return result.ToActionResult(this);
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
                return result.ToActionResult(this);
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
                return result.ToActionResult(this);
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResult<bool>.Fail(ex.Message));
            }
        }
    }
}
