namespace PBL3.Service.Storage;

public interface IStorageService
{
    Task<string> UploadAsync(Stream content, string fileName, string contentType, string folder, CancellationToken ct = default);
}
