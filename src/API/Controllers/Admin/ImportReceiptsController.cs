using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBL3.Application.ImportReceipts;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Inventory;
using PBL3.Shared.DTOs.Products;

namespace PBL3.API.Controllers.Admin
{
    [ApiController]
    [Route("api/import-receipts")]
    [Produces("application/json")]
    [Authorize(Roles = "Admin, Employee")]
    public class ImportReceiptsController : ControllerBase
    {
        private readonly IImportReceiptService _importReceiptService;

        public ImportReceiptsController(IImportReceiptService importReceiptService)
        {
            _importReceiptService = importReceiptService;
        }

        /// <summary>
        /// Tạo phiếu nhập kho mới.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResult<ImportReceiptDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResult<ImportReceiptDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CreateImportReceiptRequest request)
        {
            var result = await _importReceiptService.CreateAsync(request);

            if (!result.Success)
                return BadRequest(result);

            return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
        }

        /// <summary>
        /// Lấy danh sách phiếu nhập kho (Lịch sử nhập kho) - có phân trang và tìm kiếm.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResult<PagedResult<ImportReceiptDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetList([FromQuery] ImportReceiptFilterRequest filter)
        {
            var result = await _importReceiptService.GetPagedListAsync(filter);
            return Ok(result);
        }

        /// <summary>
        /// Xem chi tiết 1 phiếu nhập kho (bao gồm danh sách Serial đã nhập).
        /// </summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ApiResult<ImportReceiptDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<ImportReceiptDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _importReceiptService.GetByIdAsync(id);

            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }
    }
}
