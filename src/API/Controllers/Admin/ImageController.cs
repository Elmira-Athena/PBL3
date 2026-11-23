using Amazon.S3;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PBL3.API.Filters;
using PBL3.Service.Storage;
using PBL3.Shared.DTOs.Common;

namespace PBL3.API.Controllers.Admin;

[ApiController]
[Route("api/images")]
[Authorize]
public class ImageController : ControllerBase
{
    private static readonly string[] AllowedContentTypes =
        ["image/jpeg", "image/png", "image/webp", "image/gif"];

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

    private static readonly HashSet<string> AllowedFolders = new(StringComparer.OrdinalIgnoreCase)
        { "avatars", "products", "banners", "manufacturers", "categories", "general" };

    private static readonly HashSet<string> AdminOnlyFolders = new(StringComparer.OrdinalIgnoreCase)
        { "products", "banners", "manufacturers", "categories", "general" };

    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB

    private readonly IStorageService _storage;
    private readonly ILogger<ImageController> _logger;

    public ImageController(IStorageService storage, ILogger<ImageController> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    [HttpPost("upload")]
    [DbRateLimit("UploadImageRateLimit", permitLimit: 20, windowSeconds: 60)]
    [RequestSizeLimit(MaxFileSizeBytes)]
    public async Task<IActionResult> Upload(IFormFile file, [FromQuery] string folder = "general", CancellationToken ct = default)
    {
        var normalizedFolder = (folder ?? string.Empty).Trim().ToLowerInvariant();
        if (!AllowedFolders.Contains(normalizedFolder))
            return BadRequest(ApiResult<UploadImageResponse>.Fail("Thư mục lưu trữ ảnh không hợp lệ."));

        if (AdminOnlyFolders.Contains(normalizedFolder) &&
            !(User.IsInRole("Admin") || User.IsInRole("Employee")))
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                ApiResult<UploadImageResponse>.Fail("Bạn không có quyền tải ảnh lên thư mục này."));
        }

        if (file == null || file.Length == 0)
            return BadRequest(ApiResult<UploadImageResponse>.Fail("Vui lòng chọn file ảnh để upload."));

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(ApiResult<UploadImageResponse>.Fail("File ảnh không được vượt quá 5MB."));

        var ext = Path.GetExtension(file.FileName ?? string.Empty).ToLowerInvariant();
        if (!AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase) ||
            !AllowedExtensions.Contains(ext))
        {
            return BadRequest(ApiResult<UploadImageResponse>.Fail("Chỉ chấp nhận file ảnh định dạng JPEG, PNG, WebP hoặc GIF."));
        }

        try
        {
            await using var uploadStream = new MemoryStream((int)Math.Min(file.Length, MaxFileSizeBytes));
            await file.CopyToAsync(uploadStream, ct);

            if (uploadStream.Length == 0)
                return BadRequest(ApiResult<UploadImageResponse>.Fail("Vui lòng chọn file ảnh để upload."));

            if (uploadStream.Length > MaxFileSizeBytes)
                return BadRequest(ApiResult<UploadImageResponse>.Fail("File ảnh không được vượt quá 5MB."));

            uploadStream.Position = 0;
            var header = new byte[12];
            var bytesRead = uploadStream.Read(header, 0, header.Length);
            var detected = DetectImageFormat(header.AsSpan(0, bytesRead));
            if (detected is null)
            {
                return BadRequest(ApiResult<UploadImageResponse>.Fail(
                    "Nội dung file không phải định dạng ảnh hợp lệ (JPEG, PNG, WebP hoặc GIF)."));
            }

            if (!string.Equals(file.ContentType, detected.Value.ContentType, StringComparison.OrdinalIgnoreCase) ||
                !IsExtensionMatchingFormat(ext, detected.Value.Extension))
            {
                return BadRequest(ApiResult<UploadImageResponse>.Fail(
                    "Định dạng file ảnh không khớp với nội dung thực tế."));
            }

            uploadStream.Position = 0;
            var url = await _storage.UploadAsync(
                uploadStream,
                $"upload{detected.Value.Extension}",
                detected.Value.ContentType,
                normalizedFolder,
                ct);
            return Ok(ApiResult<UploadImageResponse>.Ok(new UploadImageResponse(url), "Upload ảnh thành công."));
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Lỗi S3 khi upload ảnh: {ErrorCode}", ex.ErrorCode);
            return StatusCode(503, ApiResult<UploadImageResponse>.Fail("Dịch vụ lưu trữ ảnh hiện không khả dụng. Vui lòng thử lại sau."));
        }
    }

    private static (string ContentType, string Extension)? DetectImageFormat(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return ("image/jpeg", ".jpg");

        if (header.Length >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            return ("image/png", ".png");

        if (header.Length >= 6 &&
            header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38 &&
            (header[4] == 0x37 || header[4] == 0x39) && header[5] == 0x61)
            return ("image/gif", ".gif");

        if (header.Length >= 12 &&
            header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
            return ("image/webp", ".webp");

        return null;
    }

    private static bool IsExtensionMatchingFormat(string extension, string detectedExtension)
        => detectedExtension == ".jpg"
            ? extension is ".jpg" or ".jpeg"
            : string.Equals(extension, detectedExtension, StringComparison.OrdinalIgnoreCase);
}
