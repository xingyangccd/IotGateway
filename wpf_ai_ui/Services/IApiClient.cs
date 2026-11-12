// ============================================================
// 文件：Services/IApiClient.cs
// 命名空间：IotGateway.WPF.Services
// 用途：API 客户端接口
//       定义所有向后端 API 发起的请求
//       使用接口的好处：
//         1. 方便单元测试（Mock 替换真实实现）
//         2. 方便替换实现（如从 HttpClient 换成 Refit）
// ============================================================

using IotGateway.WPF.Models;                     // 引入数据模型

namespace IotGateway.WPF.Services;

/// <summary>
/// API 客户端接口
/// 定义所有需要调用的 API 方法
/// </summary>
public interface IApiClient
{
    /// <summary>
    /// 用户登录
    /// POST /api/auth/login
    /// </summary>
    /// <param name="request">登录请求（用户名 + 密码）</param>
    /// <returns>登录响应（JWT Token + 用户信息）</returns>
    Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request);
}
