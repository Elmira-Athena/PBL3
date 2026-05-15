using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBL3.Service.BuildPc;
using PBL3.Shared.DTOs.BuildPc;
using PBL3.Shared.DTOs.Common;

namespace PBL3.API.Controllers.Admin
{
    [Route("api/build-pc")]
    [ApiController]
    public class BuildPcController : ControllerBase
    {
        private readonly IBuildPcService _buildPcService;

        public BuildPcController(IBuildPcService buildPcService)
        {
            _buildPcService = buildPcService;
        }

        [AllowAnonymous]
        [HttpPost("export")]
        public async Task<IActionResult> ExportToExcel([FromBody] ExportBuildPcRequest request)
        {
            if (request.Items == null || !request.Items.Any())
                return BadRequest(ApiResult<object>.Fail("Chưa có linh kiện nào trong cấu hình."));

            var bytes = await _buildPcService.ExportToExcelAsync(request);
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "cau-hinh-pc.xlsx");
        }
    }
}
