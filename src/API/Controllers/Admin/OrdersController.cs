using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBL3.Service.Orders;
using PBL3.Shared.DTOs.Sale;
using PBL3.Core.Exceptions;
using PBL3.Shared.DTOs.Common;

namespace PBL3.API.Controllers.Admin
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(IOrderService orderService, ILogger<OrdersController> logger)
        {
            _orderService = orderService;
            _logger = logger;
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
            catch (BusinessRuleException ex)
            {
                // Thông báo nghiệp vụ đã soạn cho người dùng — trả NGUYÊN VĂN.
                return BadRequest(ApiResult<CheckoutResponse>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                // KHÔNG relay ex.Message: tới đây ex là lỗi hạ tầng (EF Core / SQL Server),
                // nội dung tiếng Anh và lộ nội tạng ORM. Xem mục 🅴/🅷 của runbook.
                _logger.LogError(ex, "Checkout thất bại.");
                return BadRequest(ApiResult<CheckoutResponse>.Fail(
                    "Không thể hoàn tất đặt hàng do lỗi hệ thống. Vui lòng thử lại sau ít phút; nếu vẫn không được, xin liên hệ bộ phận hỗ trợ."));
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
            if (User.IsInRole("Customer"))
            {
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdStr, out var userId))
                    return Unauthorized(ApiResult<OrderDetailDto>.Fail("Không thể xác thực thông tin người dùng."));

                var myResult = await _orderService.GetMyOrderByIdAsync(id, userId);
                if (!myResult.Success) return NotFound(myResult);
                return Ok(myResult);
            }

            var result = await _orderService.GetByIdAsync(id);
            if (!result.Success)
            {
                return NotFound(result);
            }
            return Ok(result);
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
                if (!result.Success) return BadRequest(result);
                return Ok(result);
            }
            catch (BusinessRuleException ex)
            {
                // Thông báo nghiệp vụ đã soạn cho người dùng — trả NGUYÊN VĂN.
                return BadRequest(ApiResult<bool>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                // KHÔNG relay ex.Message: tới đây ex là lỗi hạ tầng (EF Core / SQL Server),
                // nội dung tiếng Anh và lộ nội tạng ORM. Xem mục 🅴/🅷 của runbook.
                _logger.LogError(ex, "Khách tự hủy đơn thất bại.");
                return BadRequest(ApiResult<bool>.Fail(
                    "Không thể hủy đơn do lỗi hệ thống. Vui lòng thử lại sau ít phút."));
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
                if (!result.Success) return BadRequest(result);
                return Ok(result);
            }
            catch (BusinessRuleException ex)
            {
                // Thông báo nghiệp vụ đã soạn cho người dùng — trả NGUYÊN VĂN.
                return BadRequest(ApiResult<bool>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                // KHÔNG relay ex.Message: tới đây ex là lỗi hạ tầng (EF Core / SQL Server),
                // nội dung tiếng Anh và lộ nội tạng ORM. Xem mục 🅴/🅷 của runbook.
                _logger.LogError(ex, "Khách xác nhận đã nhận hàng thất bại.");
                return BadRequest(ApiResult<bool>.Fail(
                    "Không thể xác nhận đã nhận hàng do lỗi hệ thống. Vui lòng thử lại sau ít phút."));
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin, Employee")]
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
            catch (BusinessRuleException ex)
            {
                // Thông báo nghiệp vụ đã soạn cho người dùng — trả NGUYÊN VĂN.
                return BadRequest(ApiResult<bool>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                // KHÔNG relay ex.Message: tới đây ex là lỗi hạ tầng (EF Core / SQL Server),
                // nội dung tiếng Anh và lộ nội tạng ORM. Xem mục 🅴/🅷 của runbook.
                _logger.LogError(ex, "Hủy đơn (nhân viên) thất bại.");
                return BadRequest(ApiResult<bool>.Fail(
                    "Không thể hủy đơn do lỗi hệ thống. Vui lòng thử lại sau ít phút."));
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
            catch (BusinessRuleException ex)
            {
                // Thông báo nghiệp vụ đã soạn cho người dùng — trả NGUYÊN VĂN.
                return BadRequest(ApiResult<bool>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                // KHÔNG relay ex.Message: tới đây ex là lỗi hạ tầng (EF Core / SQL Server),
                // nội dung tiếng Anh và lộ nội tạng ORM. Xem mục 🅴/🅷 của runbook.
                _logger.LogError(ex, "Xác nhận hoàn tất đơn thất bại.");
                return BadRequest(ApiResult<bool>.Fail(
                    "Không thể xác nhận hoàn tất đơn do lỗi hệ thống. Vui lòng thử lại sau ít phút."));
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
            catch (BusinessRuleException ex)
            {
                // Thông báo nghiệp vụ đã soạn cho người dùng — trả NGUYÊN VĂN.
                return BadRequest(ApiResult<bool>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                // KHÔNG relay ex.Message: tới đây ex là lỗi hạ tầng (EF Core / SQL Server),
                // nội dung tiếng Anh và lộ nội tạng ORM. Xem mục 🅴/🅷 của runbook.
                _logger.LogError(ex, "Duyệt đơn thất bại.");
                return BadRequest(ApiResult<bool>.Fail(
                    "Không thể duyệt đơn do lỗi hệ thống. Vui lòng thử lại sau ít phút."));
            }
        }
    }
}
