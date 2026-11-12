// ============================================================
// 文件：IotGateway.Common/Helpers/ApiResponse.cs
// 用途：统一 API 响应格式工具类
//       所有 API 返回值都使用 ApiResponse<T> 封装
// 所属层：Common（公共层）—— 被所有上层项目引用
//
// 设计对比（第 3 章 vs 第 4 章）：
//   第 3 章：返回 new { Code, Message, Data } （匿名对象，每个接口手写）
//   第 4 章：返回 ApiResponse<T>.Ok(data) （泛型 + 静态工厂方法）
//
// 好处：
//   1. 类型安全：编译时就知道返回的是 ApiResponse<T>
//   2. 代码简洁：不用每个接口写 new { Code = 200, ... }
//   3. 易于扩展：添加新状态码只需加一个静态方法
//   4. 前后端统一：WPF 端和 Vue3 端用同一套格式解析
// ============================================================

namespace IotGateway.Common.Helpers;

/// <summary>
/// 统一 API 响应格式（泛型版，推荐使用）
/// </summary>
/// <typeparam name="T">Data 字段的类型</typeparam>
public class ApiResponse<T>
{
    /// <summary>业务状态码</summary>
    public int Code { get; set; }

    /// <summary>提示消息</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>响应数据</summary>
    public T? Data { get; set; }

    /// <summary>请求追踪 ID（用于日志关联）</summary>
    public string? TraceId { get; set; }

    /// <summary>是否成功（Code == 200）</summary>
    public bool IsSuccess => Code == 200;

    // ===== 成功响应工厂方法 =====

    /// <summary>成功（有数据）</summary>
    public static ApiResponse<T> Ok(T data, string message = "操作成功")
    {
        return new ApiResponse<T> { Code = 200, Message = message, Data = data };
    }

    /// <summary>成功（无数据）</summary>
    public static ApiResponse<T> Ok(string message = "操作成功")
    {
        return new ApiResponse<T> { Code = 200, Message = message };
    }

    // ===== 失败响应工厂方法 =====

    /// <summary>业务失败</summary>
    public static ApiResponse<T> Fail(string message, int code = 400)
    {
        return new ApiResponse<T> { Code = code, Message = message };
    }

    /// <summary>未授权</summary>
    public static ApiResponse<T> Unauthorized(string message = "未授权，请先登录")
    {
        return new ApiResponse<T> { Code = 401, Message = message };
    }

    /// <summary>资源未找到</summary>
    public static ApiResponse<T> NotFound(string message = "未找到请求的资源")
    {
        return new ApiResponse<T> { Code = 404, Message = message };
    }

    /// <summary>服务器错误</summary>
    public static ApiResponse<T> Error(string message = "服务器内部错误")
    {
        return new ApiResponse<T> { Code = 500, Message = message };
    }
}

/// <summary>
/// 非泛型版（Data 为 object 类型，兼容旧版或无需强类型的场景）
/// </summary>
public class ApiResponse
{
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
    public string? TraceId { get; set; }

    public static ApiResponse Ok(object? data = null, string message = "操作成功")
    {
        return new ApiResponse { Code = 200, Message = message, Data = data };
    }

    public static ApiResponse Fail(string message, int code = 400)
    {
        return new ApiResponse { Code = code, Message = message };
    }

    public static ApiResponse Unauthorized(string message = "未授权")
    {
        return new ApiResponse { Code = 401, Message = message };
    }

    public static ApiResponse NotFound(string message = "资源未找到")
    {
        return new ApiResponse { Code = 404, Message = message };
    }

    public static ApiResponse Error(string message = "服务器内部错误")
    {
        return new ApiResponse { Code = 500, Message = message };
    }
}
