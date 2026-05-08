using PBL3.Shared.DTOs.BuildPc;

namespace Client.Services.BuildPc
{
    public interface IBuildPcClientService
    {
        Task ExportBuildPcAsync(ExportBuildPcRequest request);
    }
}
