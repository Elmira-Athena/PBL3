using Microsoft.AspNetCore.Components.Forms;

namespace Client.Services.Image;

public interface IImageClientService
{
    Task<string?> UploadAsync(IBrowserFile file, string folder);
}
