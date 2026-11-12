using System.Security.Claims;
using IotGateway.Common.Exceptions;
using IotGateway.Common.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace IotGateway.WebAPI.Filters;

[AttributeUsage(AttributeTargets.Class)]
public sealed class AlarmApiValidationAttribute : ActionFilterAttribute
{
    public AlarmApiValidationAttribute() => Order = -3000;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ModelState.IsValid)
            return;

        var errors = context.ModelState.Values.SelectMany(value => value.Errors)
            .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                ? "请求参数格式无效。" : error.ErrorMessage);
        context.Result = new BadRequestObjectResult(ApiResponse.Fail(string.Join(" ", errors)));
    }

    public static long ActorId(ClaimsPrincipal user)
    {
        if (!long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) || id <= 0)
            throw new UnauthorizedException("身份标识必须为有效的数字用户 ID。");
        return id;
    }
}
