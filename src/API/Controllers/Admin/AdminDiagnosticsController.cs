using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PBL3.Infrastructure.Data;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Diagnostics;

namespace PBL3.API.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/diagnostics")]
    [Produces("application/json")]
    [Authorize(Roles = "Admin")]
    public class AdminDiagnosticsController : ControllerBase
    {
        private const string DiagnosticsPurpose = "HushStore.AdminDiagnostics.v1";
        private const int MaxPayloadLength = 2048;

        private readonly HushStoreDbContext _primaryDb;
        private readonly ReadOnlyHushStoreDbContext _readOnlyDb;
        private readonly IDataProtectionProvider _dataProtectionProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AdminDiagnosticsController> _logger;

        public AdminDiagnosticsController(
            HushStoreDbContext primaryDb,
            ReadOnlyHushStoreDbContext readOnlyDb,
            IDataProtectionProvider dataProtectionProvider,
            IConfiguration configuration,
            ILogger<AdminDiagnosticsController> logger)
        {
            _primaryDb = primaryDb;
            _readOnlyDb = readOnlyDb;
            _dataProtectionProvider = dataProtectionProvider;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet("read-replica")]
        [ProducesResponseType(typeof(ApiResult<ReadReplicaDiagnosticResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> CheckReadReplica(CancellationToken cancellationToken)
        {
            try
            {
                var readOnlyConfigured = !string.IsNullOrWhiteSpace(
                    _configuration.GetConnectionString("ReadOnlyConnection"));

                var readOnlyInRecovery = await _readOnlyDb.Database
                    .SqlQueryRaw<bool>(@"SELECT pg_is_in_recovery() AS ""Value""")
                    .SingleAsync(cancellationToken);

                var primaryInRecovery = await _primaryDb.Database
                    .SqlQueryRaw<bool>(@"SELECT pg_is_in_recovery() AS ""Value""")
                    .SingleAsync(cancellationToken);

                var response = new ReadReplicaDiagnosticResponse
                {
                    ReadOnlyConfigured = readOnlyConfigured,
                    ReadOnlyContextInRecovery = readOnlyInRecovery,
                    PrimaryContextInRecovery = primaryInRecovery
                };

                return Ok(ApiResult<ReadReplicaDiagnosticResponse>.Ok(
                    response,
                    "Kiểm tra trạng thái Read Replica thành công."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kiểm tra trạng thái Read Replica thất bại.");
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    ApiResult<ReadReplicaDiagnosticResponse>.Fail(
                        "Không thể kiểm tra trạng thái Read Replica. Vui lòng xem log máy chủ."));
            }
        }

        [HttpPost("dataprotection-verify")]
        [ProducesResponseType(typeof(ApiResult<DataProtectionVerifyResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<DataProtectionVerifyResponse>), StatusCodes.Status400BadRequest)]
        public IActionResult VerifyDataProtection([FromBody] DataProtectionVerifyRequest? request)
        {
            var mode = (request?.Mode ?? "roundtrip").Trim().ToLowerInvariant();
            var payload = request?.Payload ?? string.Empty;

            if (string.IsNullOrEmpty(payload) || payload.Length > MaxPayloadLength)
            {
                return BadRequest(ApiResult<DataProtectionVerifyResponse>.Fail(
                    "Dữ liệu kiểm tra không hợp lệ (bắt buộc và tối đa 2048 ký tự)."));
            }

            if (mode is not ("roundtrip" or "protect" or "unprotect"))
            {
                return BadRequest(ApiResult<DataProtectionVerifyResponse>.Fail(
                    "Chế độ kiểm tra chỉ hỗ trợ: roundtrip, protect, unprotect."));
            }

            var ssmConfigured = !string.IsNullOrWhiteSpace(_configuration["DataProtection:SsmPrefix"]);

            try
            {
                var protector = _dataProtectionProvider.CreateProtector(DiagnosticsPurpose);

                switch (mode)
                {
                    case "roundtrip":
                    {
                        var protectedValue = protector.Protect(payload);
                        var unprotectedValue = protector.Unprotect(protectedValue);

                        return Ok(ApiResult<DataProtectionVerifyResponse>.Ok(
                            new DataProtectionVerifyResponse
                            {
                                SsmPrefixConfigured = ssmConfigured,
                                Mode = mode,
                                RoundTripMatched = string.Equals(unprotectedValue, payload, StringComparison.Ordinal),
                                ProtectedLength = protectedValue.Length
                            },
                            "Kiểm tra DataProtection thành công."));
                    }
                    case "protect":
                    {
                        var protectedValue = protector.Protect(payload);

                        return Ok(ApiResult<DataProtectionVerifyResponse>.Ok(
                            new DataProtectionVerifyResponse
                            {
                                SsmPrefixConfigured = ssmConfigured,
                                Mode = mode,
                                ProtectedLength = protectedValue.Length,
                                ProtectedPayload = protectedValue
                            },
                            "Mã hóa dữ liệu kiểm tra thành công."));
                    }
                    case "unprotect":
                    {
                        try
                        {
                            var unprotectedValue = protector.Unprotect(payload);

                            return Ok(ApiResult<DataProtectionVerifyResponse>.Ok(
                                new DataProtectionVerifyResponse
                                {
                                    SsmPrefixConfigured = ssmConfigured,
                                    Mode = mode,
                                    UnprotectedPayload = unprotectedValue
                                },
                                "Giải mã dữ liệu kiểm tra thành công."));
                        }
                        catch (CryptographicException ex)
                        {
                            _logger.LogWarning(ex, "Giải mã payload kiểm tra DataProtection thất bại.");
                            return BadRequest(ApiResult<DataProtectionVerifyResponse>.Fail(
                                "Không thể giải mã chuỗi dữ liệu (khóa không khớp hoặc dữ liệu không hợp lệ)."));
                        }
                    }
                    default:
                        return BadRequest(ApiResult<DataProtectionVerifyResponse>.Fail(
                            "Chế độ kiểm tra chỉ hỗ trợ: roundtrip, protect, unprotect."));
                }
            }
            catch (CryptographicException ex)
            {
                _logger.LogError(ex, "DataProtection không thể đọc hoặc ghi key ring.");
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    ApiResult<DataProtectionVerifyResponse>.Fail(
                        "Không thể thực hiện kiểm tra DataProtection. Vui lòng xem log máy chủ."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không xác định khi kiểm tra DataProtection.");
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    ApiResult<DataProtectionVerifyResponse>.Fail(
                        "Không thể thực hiện kiểm tra DataProtection. Vui lòng xem log máy chủ."));
            }
        }
    }
}
