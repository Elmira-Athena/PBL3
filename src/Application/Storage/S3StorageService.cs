using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;

namespace PBL3.Application.Storage;

public class S3StorageService(
    IAmazonS3 s3,
    IConfiguration configuration) : IStorageService
{
    private readonly IAmazonS3 _s3 =
        s3 ?? throw new ArgumentNullException(nameof(s3));
    private readonly string _bucketName =
        (configuration ?? throw new ArgumentNullException(nameof(configuration)))["AwsSettings:BucketName"]
            ?? throw new InvalidOperationException("AwsSettings:BucketName chưa được cấu hình.");
    private readonly string _region =
        (configuration ?? throw new ArgumentNullException(nameof(configuration)))["AwsSettings:Region"]
            ?? throw new InvalidOperationException("AwsSettings:Region chưa được cấu hình.");

    public async Task<string> UploadAsync(Stream content, string fileName, string contentType, string folder, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var key = $"{folder.Trim('/')}/{Guid.NewGuid()}{ext}";

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
