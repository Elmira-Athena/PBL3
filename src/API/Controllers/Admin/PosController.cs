using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBL3.Application.Pos;
using PBL3.Shared.DTOs.Pos;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using PBL3.API.Extensions;

namespace PBL3.API.Controllers.Admin
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,Employee")]
    public class PosController : ControllerBase
    {
        private readonly IPosService _posService;

        public PosController(IPosService posService)
        {
            _posService = posService;
        }

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
            return result.ToActionResult(this);
        }

        [HttpGet("customer")]
        public async Task<IActionResult> LookupCustomer([FromQuery] string phone)
        {
            var result = await _posService.LookupCustomerAsync(phone);
            return result.ToActionResult(this);
        }

        [HttpPost("voucher/validate")]
        public async Task<IActionResult> ValidateVoucher([FromQuery] string code, [FromQuery] decimal subTotal)
        {
            var result = await _posService.ValidateVoucherAsync(code, subTotal);
            return result.ToActionResult(this);
        }

        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout([FromBody] PosCheckoutRequest request)
        {
            var employeeId = GetCurrentUserId();
            var result = await _posService.CheckoutAsync(request, employeeId);
            return result.ToActionResult(this);
        }

        [HttpPost("drafts")]
        public async Task<IActionResult> SaveDraft([FromBody] PosCheckoutRequest request)
        {
            var employeeId = GetCurrentUserId();
            var result = await _posService.SaveDraftAsync(request, employeeId);
            return result.ToActionResult(this);
        }

        [HttpGet("drafts")]
        public async Task<IActionResult> GetDrafts()
        {
            var employeeId = GetCurrentUserId();
            var result = await _posService.GetDraftsAsync(employeeId);
            return result.ToActionResult(this);
        }

        [HttpGet("drafts/{id}")]
        public async Task<IActionResult> GetDraftById(int id)
        {
            var employeeId = GetCurrentUserId();
            var result = await _posService.GetDraftByIdAsync(id, employeeId);
            return result.ToActionResult(this);
        }

        [HttpDelete("drafts/{id}")]
        public async Task<IActionResult> DeleteDraft(int id)
        {
            var employeeId = GetCurrentUserId();
            var result = await _posService.DeleteDraftAsync(id, employeeId);
            return result.ToActionResult(this);
        }
    }
}
