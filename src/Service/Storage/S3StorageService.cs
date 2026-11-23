using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;

namespace PBL3.Service.Storage;

public class S3StorageService : IStorageService
{
    private static readonly HashSet<string> AllowedFolders = new(StringComparer.OrdinalIgnoreCase)
        { "avatars", "products", "banners", "manufacturers", "categories", "general" };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        { "image/jpeg", "image/png", "image/webp", "image/gif" };

    private readonly IAmazonS3 _s3;
    private readonly string _bucketName;
    private readonly string _region;

    public S3StorageService(IAmazonS3 s3, IConfiguration configuration)
    {
        _s3 = s3;
        _bucketName = configuration["AwsSettings:BucketName"]
            ?? throw new InvalidOperationException("AwsSettings:BucketName chưa được cấu hình.");
        _region = configuration["AwsSettings:Region"]
            ?? throw new InvalidOperationException("AwsSettings:Region chưa được cấu hình.");
    }

    public async Task<string> UploadAsync(Stream content, string fileName, string contentType, string folder, CancellationToken ct = default)
    {
        var cleanFolder = (folder ?? string.Empty).Trim().Trim('/').ToLowerInvariant();
        if (!AllowedFolders.Contains(cleanFolder))
            throw new ArgumentException("Thư mục lưu trữ ảnh không hợp lệ.", nameof(folder));

        var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            throw new ArgumentException("Phần mở rộng file ảnh không được phép.", nameof(fileName));

        if (string.IsNullOrWhiteSpace(contentType) || !AllowedContentTypes.Contains(contentType))
            throw new ArgumentException("Loại nội dung file ảnh không được phép.", nameof(contentType));

        var key = $"{cleanFolder}/{Guid.NewGuid()}{ext}";

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType
        };

        await _s3.PutObjectAsync(request, ct);

        return $"https://{_bucketName}.s3.{_region}.amazonaws.com/{key}";
    }
}
