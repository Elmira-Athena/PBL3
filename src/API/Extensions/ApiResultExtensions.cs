using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PBL3.Shared.DTOs.Common;

namespace PBL3.API.Extensions;

public static class ApiResultExtensions
{
    public static IActionResult ToActionResult<T>(
        this ApiResult<T> result, ControllerBase controller)
    {
        if (result.Success) return controller.Ok(result);
        return result.ErrorCode switch
        {
            ApiErrorCode.NotFound  => controller.NotFound(result),
            ApiErrorCode.Forbidden => controller.StatusCode(StatusCodes.Status403Forbidden, result),
            ApiErrorCode.Conflict  => controller.Conflict(result),
            _                      => controller.BadRequest(result),
        };
    }
}
