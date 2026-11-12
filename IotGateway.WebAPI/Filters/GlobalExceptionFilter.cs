// ============================================================
// 文件：IotGateway.WebAPI/Filters/GlobalExceptionFilter.cs
// 用途：全局异常过滤器
//       捕获所有 Controller 中未处理的异常
//       根据异常类型自动转换为统一的 ApiResponse 格式返回
// 所属层：WebAPI（表示层）
//
// 工作流程：
//   HTTP 请求 → Controller → Service → throw BusinessException("库存不足")
//     → 异常冒泡到 GlobalExceptionFilter.OnException()
//       → 判断异常类型 → 设置 HTTP 状态码 → 返回 ApiResponse JSON
//
// Filter vs Middleware 对比：
//   Filter（IExceptionFilter）：拦截 Controller 层异常
//     - 可访问 ActionContext（知道是哪个 Action 抛的）
//     - 只能捕获 Controller 方法中抛出的异常
//   Middleware：拦截所有层异常（包括中间件本身）
//     - 无法访问 ActionContext
//   本课程使用 Filter 方式，更简洁、更适合 WebAPI 场景
// ============================================================

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using IotGateway.Common.Exceptions;
using IotGateway.Common.Helpers;

namespace IotGateway.WebAPI.Filters;

/// <summary>
/// 全局异常过滤器
/// 注册方式（Program.cs）：
///   builder.Services.AddControllers(options =>
///   {
///       options.Filters.Add<GlobalExceptionFilter>();
///   });
/// </summary>
public class GlobalExceptionFilter : IExceptionFilter
{
    private readonly ILogger<GlobalExceptionFilter> _logger;

    public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 当 Controller 方法抛出未处理的异常时，ASP.NET Core 调用此方法
    /// </summary>
    public void OnException(ExceptionContext context)
    {
        ApiResponse result;
        int httpStatusCode;

        // ===== 根据异常类型返回不同的 HTTP 状态码和响应 =====
        switch (context.Exception)
        {
            // 未授权异常（用户名/密码错误）→ HTTP 401
            case UnauthorizedException ex:
                _logger.LogWarning("认证失败：{Message}", ex.Message);
                result = ApiResponse.Unauthorized(ex.Message);
                httpStatusCode = StatusCodes.Status401Unauthorized;
                break;

            // 权限不足异常 → HTTP 403
            case ForbiddenException ex:
                _logger.LogWarning("权限不足：{Message}", ex.Message);
                result = ApiResponse.Fail(ex.Message, 403);
                httpStatusCode = StatusCodes.Status403Forbidden;
                break;

            // 数据验证异常（用户名/密码为空）→ HTTP 422
            case ValidationException ex:
                _logger.LogWarning("参数验证失败：{Message}", ex.Message);
                result = ApiResponse.Fail(ex.Message, 422);
                httpStatusCode = StatusCodes.Status422UnprocessableEntity;
                break;

            // 资源未找到 → HTTP 404
            case NotFoundException ex:
                _logger.LogInformation("资源未找到：{Message}", ex.Message);
                result = ApiResponse.NotFound(ex.Message);
                httpStatusCode = StatusCodes.Status404NotFound;
                break;

            // 业务逻辑异常（通用）→ 使用异常中指定的状态码
            case BusinessException ex:
                _logger.LogWarning("业务异常 [{Code}]：{Message}", ex.StatusCode, ex.Message);
                result = ApiResponse.Fail(ex.Message, ex.StatusCode);
                httpStatusCode = ex.StatusCode;
                break;

            // 未处理的异常（代码 Bug / 数据库连接失败等）→ HTTP 500
            default:
                _logger.LogError(context.Exception,
                    "未处理的异常：{Message}", context.Exception.Message);
                result = ApiResponse.Error(
                    $"服务器内部错误：{context.Exception.Message}");
                httpStatusCode = StatusCodes.Status500InternalServerError;
                break;
        }

        // ===== 设置 HTTP 响应 =====
        context.Result = new ObjectResult(result)
        {
            StatusCode = httpStatusCode
        };
        context.ExceptionHandled = true;            // 标记异常已处理（不再向上抛出）
    }
}
