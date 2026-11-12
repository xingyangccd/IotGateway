// ============================================================
// 文件：IotGateway.Common/Exceptions/AppException.cs
// 用途：自定义异常体系
//       替代传统的 Controller 层 try-catch + 手写错误响应
// 所属层：Common（公共层）
//
// 设计思路：
//   传统方式：
//     Controller 中 try { ... } catch { return ApiResponse.Fail("xxx"); }
//     问题：每个 Controller 方法都要写 try-catch，代码重复
//
//   改进方式（自定义异常 + 全局过滤器）：
//     Service 中 throw new BusinessException("库存不足");
//     GlobalExceptionFilter 统一捕获 → 自动转换为 ApiResponse.Fail("库存不足");
//     Controller 不需要写任何 try-catch！
// ============================================================

namespace IotGateway.Common.Exceptions;

/// <summary>
/// 应用异常基类
/// 所有自定义异常继承自此基类
/// </summary>
public class AppException : Exception
{
    /// <summary>HTTP 状态码（用于 GlobalExceptionFilter 设置 HTTP 响应）</summary>
    public int StatusCode { get; set; } = 500;

    public AppException(string message) : base(message) { }

    public AppException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }
}

/// <summary>
/// 业务逻辑异常（HTTP 400）
/// 用于业务规则不满足的情况
/// 示例：throw new BusinessException("库存不足，无法出库");
/// </summary>
public class BusinessException : AppException
{
    public BusinessException(string message) : base(message, 400) { }

    public BusinessException(string message, int statusCode) : base(message, statusCode) { }
}

/// <summary>
/// 资源未找到异常（HTTP 404）
/// 示例：throw new NotFoundException("设备", 123);
/// 输出："未找到 设备（ID: 123）"
/// </summary>
public class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message, 404) { }

    public NotFoundException(string resourceName, object key)
        : base($"未找到 {resourceName}（ID: {key}）", 404) { }
}

/// <summary>
/// 认证/未授权异常（HTTP 401）
/// 示例：throw new UnauthorizedException("用户名或密码错误");
/// </summary>
public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "未授权访问") : base(message, 401) { }
}

/// <summary>
/// 权限不足异常（HTTP 403）
/// 示例：throw new ForbiddenException("仅管理员可执行此操作");
/// </summary>
public class ForbiddenException : AppException
{
    public ForbiddenException(string message = "权限不足") : base(message, 403) { }
}

/// <summary>
/// 数据验证异常（HTTP 422）
/// 示例：throw new ValidationException("用户名不能为空");
/// </summary>
public class ValidationException : AppException
{
    public ValidationException(string message) : base(message, 422) { }
}
