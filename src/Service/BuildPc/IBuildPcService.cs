using PBL3.Shared.DTOs.BuildPc;

namespace PBL3.Service.BuildPc
{
    public interface IBuildPcService
    {
        Task<byte[]> ExportToExcelAsync(ExportBuildPcRequest request);
    }
}
