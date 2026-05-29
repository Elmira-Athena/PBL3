using Amazon.S3;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PBL3.Application.Storage;
using PBL3.Shared.DTOs.Common;

namespace PBL3.API.Controllers.Admin;

[ApiController]
[Route("api/images")]
[Authorize]
public class ImageController : ControllerBase
{
    private static readonly string[] AllowedContentTypes =
        ["image/jpeg", "image/png", "image/webp", "image/gif"];

    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB

    private readonly IStorageService _storage;
    private readonly ILogger<ImageController> _logger;

    public ImageController(IStorageService storage, ILogger<ImageController> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    public async Task<IActionResult> Upload(IFormFile file, [FromQuery] string folder = "general", CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResult<UploadImageResponse>.Fail("Vui lòng chọn file ảnh để upload."));

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(ApiResult<UploadImageResponse>.Fail("File ảnh không được vượt quá 5MB."));

        if (!AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            return BadRequest(ApiResult<UploadImageResponse>.Fail("Chỉ chấp nhận file ảnh định dạng JPEG, PNG, WebP hoặc GIF."));

        try
        {
            using var stream = file.OpenReadStream();
            var url = await _storage.UploadAsync(stream, file.FileName, file.ContentType, folder, ct);
            return Ok(ApiResult<UploadImageResponse>.Ok(new UploadImageResponse(url), "Upload ảnh thành công."));
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Lỗi S3 khi upload ảnh: {ErrorCode}", ex.ErrorCode);
            return StatusCode(503, ApiResult<UploadImageResponse>.Fail("Dịch vụ lưu trữ ảnh hiện không khả dụng. Vui lòng thử lại sau."));
        }
    }
}
