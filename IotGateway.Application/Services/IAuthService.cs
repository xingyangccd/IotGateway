// ============================================================
// 文件：IotGateway.Application/Services/IAuthService.cs
// 用途：认证服务接口
// 所属层：Application（应用层）
// ============================================================

using IotGateway.Application.DTOs;

namespace IotGateway.Application.Services;

/// <summary>
/// 认证服务接口
/// 定义认证相关的业务操作方法
/// </summary>
public interface IAuthService
{
    /// <summary>用户登录</summary>
    /// <param name="request">登录请求（用户名 + 密码）</param>
    /// <returns>登录响应（JWT Token + 用户信息）</returns>
    Task<LoginResponseDto> LoginAsync(LoginRequestDto request);
}
