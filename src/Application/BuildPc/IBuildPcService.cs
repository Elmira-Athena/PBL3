using PBL3.Shared.DTOs.BuildPc;

namespace PBL3.Application.BuildPc
{
    public interface IBuildPcService
    {
        Task<byte[]> ExportToExcelAsync(ExportBuildPcRequest request);
    }
}
