// ============================================================
// 文件：IotGateway.WebAPI/Controllers/AuthController.cs
// 用途：认证控制器（重构版）
// 所属层：WebAPI（表示层）
//
// 第 3 章版本 vs 第 4 章版本对比：
//   Before：硬编码用户列表 + 明文密码比对 + 返回匿名对象
//   After：EF Core 查数据库 + BCrypt 验证哈希 + 统一 ApiResponse<T>
//
// Controller 的职责（重构后）：
//   1. 接收 HTTP 请求（[FromBody] 自动反序列化 JSON）
//   2. 调用 Application 层 Service
//   3. 返回统一格式的 ApiResponse<T>
//   异常处理 → 完全交给 GlobalExceptionFilter（Controller 不需要 try-catch）
// ============================================================

using Microsoft.AspNetCore.Mvc;
using IotGateway.Application.DTOs;
using IotGateway.Application.Services;
using IotGateway.Common.Helpers;

namespace IotGateway.WebAPI.Controllers;

/// <summary>
/// 认证控制器
/// 路由前缀：api/auth
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    /// <summary>
    /// 构造函数
    /// IAuthService 由 AutoFac DI 容器自动注入
    /// </summary>
    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// 用户登录
    /// POST /api/auth/login
    ///
    /// 请求体示例：
    /// {
    ///     "username": "admin",
    ///     "password": "admin123"
    /// }
    ///
    /// 响应示例（成功）：
    /// {
    ///     "code": 200,
    ///     "message": "登录成功",
    ///     "data": {
    ///         "token": "eyJhbGciOi...",
    ///         "expiresAt": 1723456789,
    ///         "user": { "id": 1, "username": "admin", "realName": "系统管理员", ... }
    ///     }
    /// }
    ///
    /// 响应示例（失败）：
    /// {
    ///     "code": 401,
    ///     "message": "用户名或密码错误",
    ///     "data": null
    /// }
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        // ===== 调用 Service 层处理登录 =====
        // Service 内部：
        //   1. 参数校验（用户名/密码非空）
        //   2. 查数据库（EF Core + Repository）
        //   3. BCrypt 密码验证
        //   4. 生成 JWT Token
        //   5. 更新最后登录时间 + 记录审计日志
        // 任何步骤出错 → 抛出 AppException 子类 → GlobalExceptionFilter 自动处理
        var result = await _authService.LoginAsync(request);

        // ===== 返回统一格式响应 =====
        return Ok(ApiResponse<LoginResponseDto>.Ok(result, "登录成功"));
        // Ok() 设置 HTTP 200
        // ApiResponse<T>.Ok() 包装为统一格式
    }
}
