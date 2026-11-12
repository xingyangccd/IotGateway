// ============================================================
// 文件：IotGateway.Application/DTOs/AuthDto.cs
// 用途：认证相关的数据传输对象（DTO - Data Transfer Object）
// 所属层：Application（应用层）
//
// DTO 的作用：
//   1. 定义前端和后端交互的数据格式
//   2. 隐藏 Entity 的内部字段（如 password_hash 永远不会返回给前端）
//   3. 合并多个 Entity 的数据（如登录响应包含 Token + User）
// ============================================================

namespace IotGateway.Application.DTOs;

/// <summary>
/// 登录请求 DTO
/// </summary>
public class LoginRequestDto
{
    /// <summary>用户名</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>密码（明文，传输时应使用 HTTPS 加密）</summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// 登录响应 DTO
/// </summary>
public class LoginResponseDto
{
    /// <summary>JWT Token 字符串</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Token 过期时间（Unix 时间戳，秒）</summary>
    public long ExpiresAt { get; set; }

    /// <summary>登录用户信息</summary>
    public UserDto User { get; set; } = new();
}

/// <summary>
/// 用户信息 DTO（返回给前端的用户数据）
/// 注意：不包含 password_hash 等敏感字段
/// </summary>
public class UserDto
{
    /// <summary>用户 ID</summary>
    public long Id { get; set; }

    /// <summary>用户名</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>真实姓名</summary>
    public string RealName { get; set; } = string.Empty;

    /// <summary>邮箱</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>手机号</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>头像 URL</summary>
    public string AvatarUrl { get; set; } = string.Empty;

    /// <summary>角色</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>最后登录时间</summary>
    public DateTime? LastLoginAt { get; set; }
}
