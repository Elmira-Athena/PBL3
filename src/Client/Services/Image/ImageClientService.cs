using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;
using PBL3.Shared.DTOs.Common;

namespace Client.Services.Image;

public class ImageClientService : IImageClientService
{
    private readonly HttpClient _httpClient;
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB

    public ImageClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string?> UploadAsync(IBrowserFile file, string folder)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            using var stream = file.OpenReadStream(MaxFileSizeBytes);
            var streamContent = new StreamContent(stream);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
            content.Add(streamContent, "file", file.Name);

            var response = await _httpClient.PostAsync($"api/images/upload?folder={Uri.EscapeDataString(folder)}", content);
            if (!response.IsSuccessStatusCode)
                return null;

            var result = await response.Content.ReadFromJsonAsync<ApiResult<UploadImageResponse>>();
            return result?.Data?.Url;
        }
        catch
        {
            return null;
        }
    }
}
