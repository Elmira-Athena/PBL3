using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBL3.Application.Inventory;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Inventory;
using System.Security.Claims;
using PBL3.API.Extensions;

namespace PBL3.API.Controllers.Admin
{
    [ApiController]
    [Route("api/inventory-checks")]
    [Produces("application/json")]
    [Authorize(Roles = "Admin, Employee")]
    public class InventoryChecksController : ControllerBase
    {
        private readonly IInventoryCheckService _checkService;
        private readonly IValidator<CreateInventoryCheckRequest> _createValidator;
        private readonly IValidator<ScanSerialRequest> _scanValidator;
        private readonly IValidator<UpdateScanReasonRequest> _reasonValidator;
        private readonly IValidator<RejectInventoryCheckRequest> _rejectValidator;

        public InventoryChecksController(
            IInventoryCheckService checkService,
            IValidator<CreateInventoryCheckRequest> createValidator,
            IValidator<ScanSerialRequest> scanValidator,
            IValidator<UpdateScanReasonRequest> reasonValidator,
            IValidator<RejectInventoryCheckRequest> rejectValidator)
        {
            _checkService = checkService;
            _createValidator = createValidator;
            _scanValidator = scanValidator;
            _reasonValidator = reasonValidator;
            _rejectValidator = rejectValidator;
        }

        // ─── GET LIST ───
        [HttpGet]
        [ProducesResponseType(typeof(ApiResult<PagedResult<InventoryCheckListItemDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetList([FromQuery] InventoryCheckFilterRequest filter)
        {
            var result = await _checkService.GetPagedListAsync(filter);
            return Ok(result);
        }

        // ─── GET BY ID ───
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ApiResult<InventoryCheckDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<InventoryCheckDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _checkService.GetByIdAsync(id);
            return result.ToActionResult(this);
        }

        // ─── DASHBOARD ───
        [HttpGet("{id:int}/dashboard")]
        [ProducesResponseType(typeof(ApiResult<InventoryCheckDashboardDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDashboard(int id)
        {
            var result = await _checkService.GetDashboardAsync(id);
            return result.ToActionResult(this);
        }

        // ─── GET SERIALS ───
        [HttpGet("{id:int}/serials")]
        [ProducesResponseType(typeof(ApiResult<PagedResult<InventoryCheckSerialDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSerials(int id, [FromQuery] InventoryCheckSerialFilterRequest filter)
        {
            var result = await _checkService.GetSerialsAsync(id, filter);
            return result.ToActionResult(this);
        }

        // ─── CREATE ───
        [HttpPost]
        [ProducesResponseType(typeof(ApiResult<InventoryCheckDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResult<InventoryCheckDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CreateInventoryCheckRequest request)
        {
            var validation = await _createValidator.ValidateAsync(request);
            if (!validation.IsValid)
            {
                var errors = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
                return BadRequest(ApiResult<InventoryCheckDto>.Fail(errors));
            }

            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(ApiResult<InventoryCheckDto>.Fail("Người dùng chưa đăng nhập."));

            var result = await _checkService.CreateAsync(request, userId.Value);
            if (!result.Success) return result.ToActionResult(this);

            return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
        }

        // ─── SCAN SERIAL ───
        [HttpPost("{id:int}/scan")]
        [ProducesResponseType(typeof(ApiResult<ScanResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<ScanResultDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ScanSerial(int id, [FromBody] ScanSerialRequest request)
        {
            var validation = await _scanValidator.ValidateAsync(request);
            if (!validation.IsValid)
            {
                var errors = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
                return BadRequest(ApiResult<ScanResultDto>.Fail(errors));
            }

            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(ApiResult<ScanResultDto>.Fail("Người dùng chưa đăng nhập."));

            var result = await _checkService.ScanSerialAsync(id, request, userId.Value);
            if (!result.Success && result.Data == null)
                return result.ToActionResult(this);
            return Ok(result);
        }

        // ─── MARK DEFECTIVE ───
        [HttpPut("{id:int}/serials/{detailSerialId:int}/mark-defective")]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> MarkDefective(int id, int detailSerialId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(ApiResult<bool>.Fail("Người dùng chưa đăng nhập."));

            var result = await _checkService.MarkDefectiveAsync(id, detailSerialId, userId.Value);
            return result.ToActionResult(this);
        }

        // ─── UPDATE REASON ───
        [HttpPut("{id:int}/serials/{detailSerialId:int}/reason")]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateReason(int id, int detailSerialId, [FromBody] UpdateScanReasonRequest request)
        {
            var validation = await _reasonValidator.ValidateAsync(request);
            if (!validation.IsValid)
            {
                var errors = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
                return BadRequest(ApiResult<bool>.Fail(errors));
            }

            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(ApiResult<bool>.Fail("Người dùng chưa đăng nhập."));

            var result = await _checkService.UpdateReasonAsync(id, detailSerialId, request, userId.Value);
            return result.ToActionResult(this);
        }

        // ─── SUBMIT ───
        [HttpPost("{id:int}/submit")]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Submit(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(ApiResult<bool>.Fail("Người dùng chưa đăng nhập."));

            var result = await _checkService.SubmitAsync(id, userId.Value);
            return result.ToActionResult(this);
        }

        // ─── APPROVE (Admin only) ───
        [HttpPost("{id:int}/approve")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Approve(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(ApiResult<bool>.Fail("Người dùng chưa đăng nhập."));

            var result = await _checkService.ApproveAsync(id, userId.Value);
            return result.ToActionResult(this);
        }

        // ─── REJECT (Admin only) ───
        [HttpPost("{id:int}/reject")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Reject(int id, [FromBody] RejectInventoryCheckRequest request)
        {
            var validation = await _rejectValidator.ValidateAsync(request);
            if (!validation.IsValid)
            {
                var errors = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
                return BadRequest(ApiResult<bool>.Fail(errors));
            }

            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(ApiResult<bool>.Fail("Người dùng chưa đăng nhập."));

            var result = await _checkService.RejectAsync(id, request, userId.Value);
            return result.ToActionResult(this);
        }

        // ─── CANCEL ───
        [HttpPost("{id:int}/cancel")]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Cancel(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(ApiResult<bool>.Fail("Người dùng chưa đăng nhập."));

            var isAdmin = User.IsInRole("Admin");
            var result = await _checkService.CancelAsync(id, userId.Value, isAdmin);
            return result.ToActionResult(this);
        }

        // ─── HELPERS ───
        private Guid? GetCurrentUserId()
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(idStr) || !Guid.TryParse(idStr, out var userId))
                return null;
            return userId;
        }
    }
}
