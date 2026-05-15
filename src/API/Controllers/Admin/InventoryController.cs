using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PBL3.Service.Inventory;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Inventory;

namespace PBL3.API.Controllers.Admin
{
    [ApiController]
    [Route("api/inventory")]
    [Produces("application/json")]
    [Authorize(Roles = "Admin, Employee")]
    public class InventoryController : ControllerBase
    {
        private readonly IInventoryExportService _inventoryExportService;

        public InventoryController(IInventoryExportService inventoryExportService)
        {
            _inventoryExportService = inventoryExportService;
        }

        /// <summary>
        /// Xuất kho cho đơn hàng Online (Quét mã Serial).
        /// Chuyển trạng thái đơn hàng sang "Đang giao hàng" (Shipping).
        /// </summary>
        [HttpPost("export-order")]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ExportOrder([FromBody] ExportOrderRequest request)
        {
            var result = await _inventoryExportService.ExportOrderAsync(request);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        [HttpGet("serials/validate")]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ValidateSerial([FromQuery] string serialNo, [FromQuery] int variantId)
        {
            if (string.IsNullOrWhiteSpace(serialNo) || variantId <= 0)
                return BadRequest(ApiResult<bool>.Fail("Tham số không hợp lệ."));

            var result = await _inventoryExportService.ValidateSerialAsync(serialNo, variantId);

            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }
    }
}
