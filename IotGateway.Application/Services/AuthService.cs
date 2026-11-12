// ============================================================
// 文件：IotGateway.Application/Services/AuthService.cs
// 用途：认证服务实现（替代第 3 章 AuthController 中的硬编码逻辑）
// 所属层：Application（应用层）
//
// 为什么把登录逻辑从 Controller 移到 Service？
//   1. 单一职责原则：
//      Controller：接收 HTTP 请求 + 返回 HTTP 响应（表示层）
//      Service：业务逻辑（验证用户、生成 Token）
//   2. 可复用：未来如 WPF 客户端需要直连验证，可复用 AuthService
//   3. 可测试：Service 可以脱离 HTTP 上下文独立进行单元测试
//
// 第 3 章 vs 第 4 章对比：
//   第 3 章：AuthController.ValidateUser() 硬编码 Dictionary 对比明文密码
//   第 4 章：AuthService.LoginAsync() EF Core 查数据库 + BCrypt 验证哈希
// ============================================================

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using AutoMapper;
using IotGateway.Application.DTOs;
using IotGateway.Application.Mappings;
using IotGateway.Common.Exceptions;
using IotGateway.Domain.Entities;
using IotGateway.Infrastructure.Repositories;

namespace IotGateway.Application.Services;

/// <summary>
/// 认证服务实现
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly IMapper _mapper;
    private readonly ILogger<AuthService> _logger;

    /// <summary>
    /// 构造函数（依赖注入）
    /// IUnitOfWork：数据访问（通过 Repository 查数据库）
    /// IConfiguration：读取 JWT 配置（Key, Issuer, Audience）
    /// IMapper：AutoMapper（User 实体 → UserDto）
    /// ILogger：Serilog 结构化日志
    /// </summary>
    public AuthService(
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        IMapper mapper,
        ILogger<AuthService> logger)
    {
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _mapper = mapper;
        _logger = logger;
    }

    /// <summary>
    /// 用户登录（完整流程）
    /// </summary>
    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request)
    {
        // ===== 第 1 步：参数校验 =====
        if (string.IsNullOrWhiteSpace(request.Username))
            throw new ValidationException("用户名不能为空");
            // 抛出 ValidationException → GlobalExceptionFilter 捕获 → 返回 ApiResponse Code=422

        if (string.IsNullOrWhiteSpace(request.Password))
            throw new ValidationException("密码不能为空");

        // ===== 第 2 步：从数据库查询用户 =====
        var user = await _unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username.Trim());
        // LINQ 表达式 u => u.Username == request.Username.Trim()
        // EF Core 翻译为：SELECT * FROM users WHERE username = 'admin' LIMIT 1

        if (user == null)
        {
            _logger.LogWarning("登录失败：用户 {Username} 不存在", request.Username);
            throw new UnauthorizedException("用户名或密码错误");
            // 安全考虑：不明确提示"用户不存在"，防止账号枚举攻击
        }

        // ===== 第 3 步：检查账户状态 =====
        if (user.Status != 1)                       // 0 = 禁用
        {
            _logger.LogWarning("登录失败：用户 {Username} 已被禁用", request.Username);
            throw new UnauthorizedException("账户已被禁用，请联系管理员");
        }

        // ===== 第 4 步：BCrypt 密码验证 =====
        // user.PasswordHash 格式：$2a$11$...（BCrypt 哈希字符串）
        // BCrypt.Verify(明文密码, 哈希值) → true/false
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("登录失败：用户 {Username} 密码错误", request.Username);
            throw new UnauthorizedException("用户名或密码错误");
        }

        // ===== 第 5 步：生成 JWT Token =====
        var token = GenerateJwtToken(user);
        var expireHours = _configuration.GetValue<int>("Jwt:ExpireHours", 8);
        var expiresAt = DateTimeOffset.UtcNow.AddHours(expireHours).ToUnixTimeSeconds();

        // ===== 第 6 步：更新最后登录时间 + 记录审计日志 =====
        user.LastLoginAt = DateTime.UtcNow;
        _unitOfWork.Users.Update(user);

        var auditLog = new AuditLog
        {
            UserId = user.Id,
            Username = user.Username,
            ActionType = "Login",
            TargetType = "User",
            TargetId = user.Id.ToString(),
            Description = $"用户 {user.Username} 登录系统",
            RequestIp = "127.0.0.1",                // 实际应从 HttpContext 获取
            Result = "Success",
            CreatedAt = DateTime.UtcNow
        };
        await _unitOfWork.AuditLogs.AddAsync(auditLog);

        // 提交所有变更（用户 + 审计日志在同一事务中）
        await _unitOfWork.SaveChangesAsync();

        // ===== 第 7 步：构建响应 =====
        var userDto = _mapper.Map<UserDto>(user);   // AutoMapper 自动映射

        _logger.LogInformation("用户 {Username}（{Role}）登录成功", user.Username, user.Role);

        return new LoginResponseDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            User = userDto
        };
    }

    /// <summary>
    /// 生成 JWT Token
    /// 从第 3 章 AuthController 迁移而来，逻辑不变
    /// </summary>
    private string GenerateJwtToken(User user)
    {
        var jwtKey = _configuration["Jwt:Key"]
            ?? "IotGateway_SuperSecret_Key_2024_Must_Be_At_Least_32_Chars!";

        // 创建 Claims（Token 中携带的用户身份信息）
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("RealName", user.RealName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
        };

        // 创建签名密钥
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // 组装 JWT Token
        var expireHours = _configuration.GetValue<int>("Jwt:ExpireHours", 8);
        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "IotGateway",
            audience: _configuration["Jwt:Audience"] ?? "IotGateway_Client",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expireHours),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
