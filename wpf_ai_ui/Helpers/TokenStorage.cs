// ============================================================
// 文件：Helpers/TokenStorage.cs
// 命名空间：IotGateway.WPF.Helpers
// 用途：JWT Token 的本地安全存储
//       将 Token 持久化到本地文件，应用重启后无需重新登录
// ============================================================

using System.IO;                                 // 文件操作
using System.Security.Cryptography;              // 加密算法（ProtectedData）
using System.Text;                               // 文本编码
using System.Text.Json;                          // JSON 序列化

namespace IotGateway.WPF.Helpers;

/// <summary>
/// Token 存储帮助类
/// 负责 JWT Token 的加密存储、读取和清除
///
/// 存储位置：%LocalAppData%/IotGateway/token.dat
/// 加密方式：Windows DPAPI（Data Protection API），基于当前用户加密
/// </summary>
public static class TokenStorage
{
    // ============================================================
    // 私有字段
    // ============================================================

    /// <summary>Token 存储文件的完整路径</summary>
    private static readonly string TokenFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IotGateway",                            // 应用数据目录名
        "token.dat"                              // Token 文件名
    );

    /// <summary>内存中缓存的 Token 信息</summary>
    private static TokenInfo? _cachedToken;

    // ============================================================
    // 公共方法
    // ============================================================

    /// <summary>
    /// 保存 JWT Token 到本地
    /// </summary>
    /// <param name="token">JWT Token 字符串</param>
    /// <param name="expiresAt">过期时间</param>
    public static void SaveToken(string token, DateTime expiresAt)
    {
        // 1. 构建 Token 信息对象
        var tokenInfo = new TokenInfo
        {
            Token = token,                       // JWT Token 原始字符串
            ExpiresAt = expiresAt,               // 过期时间
            SavedAt = DateTime.Now               // 保存时间（用于调试）
        };

        // 2. 序列化为 JSON 字符串
        var json = JsonSerializer.Serialize(tokenInfo);

        // 3. 使用 DPAPI 加密（Windows 数据保护 API）
        //    DataProtectionScope.CurrentUser：只有当前 Windows 用户可以解密
        var plainBytes = Encoding.UTF8.GetBytes(json);            // JSON → 字节数组
        var encryptedBytes = ProtectedData.Protect(               // 加密
            plainBytes,                           // 原始数据
            null,                                 // 不使用额外熵（entropy）
            DataProtectionScope.CurrentUser       // 作用域：当前用户
        );

        // 4. 确保目录存在
        var directory = Path.GetDirectoryName(TokenFilePath)!;    // 获取目录路径
        if (!Directory.Exists(directory))                         // 如果目录不存在
        {
            Directory.CreateDirectory(directory);                 // 创建目录
        }

        // 5. 将加密后的数据写入文件
        File.WriteAllBytes(TokenFilePath, encryptedBytes);

        // 6. 缓存到内存中（避免频繁读文件）
        _cachedToken = tokenInfo;
    }

    /// <summary>
    /// 从本地读取 JWT Token
    /// </summary>
    /// <returns>Token 信息，如果不存在或已过期则返回 null</returns>
    public static TokenInfo? GetToken()
    {
        // 1. 优先使用内存缓存（速度快）
        if (_cachedToken != null)
        {
            // 如果缓存的 Token 未过期，直接返回
            if (_cachedToken.ExpiresAt > DateTime.Now)
                return _cachedToken;

            // 如果已过期，清除缓存
            _cachedToken = null;
        }

        // 2. 检查文件是否存在
        if (!File.Exists(TokenFilePath))
            return null;                         // 文件不存在，返回 null

        try
        {
            // 3. 读取加密数据
            var encryptedBytes = File.ReadAllBytes(TokenFilePath);

            // 4. DPAPI 解密
            var plainBytes = ProtectedData.Unprotect(            // 解密
                encryptedBytes,                   // 加密数据
                null,                             // 不使用额外熵
                DataProtectionScope.CurrentUser   // 作用域：当前用户
            );

            // 5. JSON 反序列化
            var json = Encoding.UTF8.GetString(plainBytes);     // 字节数组 → JSON 字符串
            var tokenInfo = JsonSerializer.Deserialize<TokenInfo>(json);  // JSON → 对象

            // 6. 检查是否过期
            if (tokenInfo != null && tokenInfo.ExpiresAt > DateTime.Now)
            {
                _cachedToken = tokenInfo;        // 写入内存缓存
                return tokenInfo;                // 返回有效 Token
            }
        }
        catch
        {
            // 如果解密/反序列化失败（文件损坏等），返回 null
            // 用户需要重新登录
        }

        return null;                             // Token 无效或已过期
    }

    /// <summary>
    /// 清除本地保存的 Token（退出登录时调用）
    /// </summary>
    public static void ClearToken()
    {
        _cachedToken = null;                     // 清除内存缓存

        // 删除本地文件（如果存在）
        if (File.Exists(TokenFilePath))
        {
            File.Delete(TokenFilePath);          // 删除 Token 文件
        }
    }

    /// <summary>
    /// 判断 Token 是否有效（存在且未过期）
    /// </summary>
    public static bool IsTokenValid()
    {
        var token = GetToken();                  // 获取 Token
        return token != null                     // Token 存在
            && token.ExpiresAt > DateTime.Now;   // 且未过期
    }
}

/// <summary>
/// Token 信息数据类
/// </summary>
public class TokenInfo
{
    /// <summary>JWT Token 字符串，如 "eyJhbGciOi..."</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Token 过期时间</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Token 保存时间（用于日志/调试）</summary>
    public DateTime SavedAt { get; set; }
}
