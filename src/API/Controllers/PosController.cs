using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBL3.Service.Pos;
using PBL3.Shared.DTOs.Pos;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace PBL3.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,Employee")]
    public class PosController(IPosService posService) : ControllerBase
    {
        private readonly IPosService _posService = posService;

        private Guid GetCurrentUserId()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(idClaim, out Guid id)) return id;
            return Guid.Empty;
        }

        [HttpPost("scan")]
        public async Task<IActionResult> ScanSerial([FromBody] PosScanRequest request)
        {
            var result = await _posService.ScanSerialAsync(request.SerialNumber);
            if (result.Success) return Ok(result);
            return BadRequest(result); // Return standard ApiResult JSON
        }

        [HttpGet("customer")]
        public async Task<IActionResult> LookupCustomer([FromQuery] string phone)
        {
            var result = await _posService.LookupCustomerAsync(phone);
            if (result.Success) return Ok(result);
            return BadRequest(result);
        }

        [HttpPost("voucher/validate")]
        public async Task<IActionResult> ValidateVoucher([FromQuery] string code, [FromQuery] decimal subTotal)
        {
            var result = await _posService.ValidateVoucherAsync(code, subTotal);
            if (result.Success) return Ok(result);
            return BadRequest(result);
        }

        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout([FromBody] PosCheckoutRequest request)
        {
            var employeeId = GetCurrentUserId();
            var result = await _posService.CheckoutAsync(request, employeeId);
            if (result.Success) return Ok(result);
            return BadRequest(result);
        }

        [HttpPost("drafts")]
        public async Task<IActionResult> SaveDraft([FromBody] PosCheckoutRequest request)
        {
            var employeeId = GetCurrentUserId();
            var result = await _posService.SaveDraftAsync(request, employeeId);
            if (result.Success) return Ok(result);
            return BadRequest(result);
        }

        [HttpGet("drafts")]
        public async Task<IActionResult> GetDrafts()
        {
            var employeeId = GetCurrentUserId();
            var result = await _posService.GetDraftsAsync(employeeId);
            if (result.Success) return Ok(result);
            return BadRequest(result);
        }

        [HttpGet("drafts/{id}")]
        public async Task<IActionResult> GetDraftById(int id)
        {
            var employeeId = GetCurrentUserId();
            var result = await _posService.GetDraftByIdAsync(id, employeeId);
            if (result.Success) return Ok(result);
            return BadRequest(result);
        }

        [HttpDelete("drafts/{id}")]
        public async Task<IActionResult> DeleteDraft(int id)
        {
            var employeeId = GetCurrentUserId();
            var result = await _posService.DeleteDraftAsync(id, employeeId);
            if (result.Success) return Ok(result);
            return BadRequest(result);
        }
    }
}
