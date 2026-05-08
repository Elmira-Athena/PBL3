using Microsoft.JSInterop;
using PBL3.Shared.DTOs.BuildPc;
using System.Net.Http.Json;

namespace Client.Services.BuildPc
{
    public class BuildPcClientService : IBuildPcClientService
    {
        private readonly HttpClient _httpClient;
        private readonly IJSRuntime _jsRuntime;

        public BuildPcClientService(HttpClient httpClient, IJSRuntime jsRuntime)
        {
            _httpClient = httpClient;
            _jsRuntime = jsRuntime;
        }

        public async Task ExportBuildPcAsync(ExportBuildPcRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync("/api/build-pc/export", request);
            if (!response.IsSuccessStatusCode) return;

            var bytes = await response.Content.ReadAsByteArrayAsync();
            var base64 = Convert.ToBase64String(bytes);
            await _jsRuntime.InvokeVoidAsync("downloadFile",
                "cau-hinh-pc.xlsx",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                base64);
        }
    }
}
