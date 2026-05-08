using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;

namespace PBL3.Service.Storage;

public class S3StorageService : IStorageService
{
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
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var key = $"{folder.Trim('/')}/{Guid.NewGuid()}{ext}";

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            CannedACL = S3CannedACL.PublicRead
        };

        await _s3.PutObjectAsync(request, ct);

        return $"https://{_bucketName}.s3.{_region}.amazonaws.com/{key}";
    }
}
