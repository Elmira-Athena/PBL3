using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBL3.Application.Customers;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Customers;
using PBL3.Shared.DTOs.Products;
using System;
using System.Threading.Tasks;

namespace PBL3.API.Controllers.Admin
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize(Roles = "Admin, Employee")]
    public class CustomersController : ControllerBase
    {
        private readonly ICustomerService _customerService;

        public CustomersController(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        /// <summary>
        /// Lấy danh sách khách hàng (phân trang, tìm kiếm theo Tên, Email, SĐT).
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResult<PagedResult<CustomerDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetList([FromQuery] CustomerFilterRequest filter)
        {
            var result = await _customerService.GetPagedListAsync(filter);
            return Ok(result);
        }

        /// <summary>
        /// Lấy chi tiết khách hàng theo Id (bao gồm 10 đơn hàng gần nhất + giỏ hàng).
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ApiResult<CustomerDetailDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<CustomerDetailDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _customerService.GetByIdAsync(id);

            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }

        /// <summary>
        /// Tạo mới tài khoản khách hàng (Admin tạo, hệ thống tự sinh mật khẩu).
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResult<CustomerDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResult<CustomerDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request)
        {
            var result = await _customerService.CreateAsync(request);

            if (!result.Success)
                return BadRequest(result);

            return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
        }

        /// <summary>
        /// Cập nhật thông tin khách hàng (không cho sửa Email/SĐT).
        /// </summary>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(ApiResult<CustomerDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<CustomerDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResult<CustomerDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCustomerRequest request)
        {
            var result = await _customerService.UpdateAsync(id, request);

            if (!result.Success)
            {
                if (result.Message.Contains("Không tìm thấy"))
                    return NotFound(result);

                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Khóa tài khoản khách hàng (Deactivate + thu hồi RefreshToken).
        /// </summary>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Deactivate(Guid id, [FromQuery] string? lockReason = null)
        {
            var result = await _customerService.DeactivateAsync(id, lockReason);

            if (!result.Success)
            {
                if (result.Message.Contains("Không tìm thấy"))
                    return NotFound(result);

                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Mở khóa tài khoản khách hàng.
        /// </summary>
        [HttpPut("{id:guid}/activate")]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Reactivate(Guid id)
        {
            var result = await _customerService.ReactivateAsync(id);

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
