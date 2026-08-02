using Microsoft.AspNetCore.Mvc;

namespace VK.API.Common;

public static class ServiceResultExtensions
{
    public static IActionResult ToActionResult(this ServiceResult result) => result.Status switch
    {
        ServiceResultStatus.Success => new OkResult(),
        ServiceResultStatus.NotFound => new NotFoundObjectResult(new { message = result.Message }),
        ServiceResultStatus.BadRequest => new BadRequestObjectResult(new { message = result.Message }),
        ServiceResultStatus.Forbidden => new ObjectResult(new { message = result.Message }) { StatusCode = 403 },
        ServiceResultStatus.Error => new ObjectResult(new { message = result.Message }) { StatusCode = 500 },
        _ => new StatusCodeResult(500)
    };

    public static IActionResult ToActionResult<T>(this ServiceResult<T> result) => result.Status switch
    {
        ServiceResultStatus.Success => new OkObjectResult(result.Data),
        ServiceResultStatus.NotFound => new NotFoundObjectResult(new { message = result.Message }),
        ServiceResultStatus.BadRequest => new BadRequestObjectResult(new { message = result.Message }),
        ServiceResultStatus.Forbidden => new ObjectResult(new { message = result.Message }) { StatusCode = 403 },
        ServiceResultStatus.Error => new ObjectResult(new { message = result.Message }) { StatusCode = 500 },
        _ => new StatusCodeResult(500)
    };
}