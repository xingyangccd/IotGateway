// ============================================================
// 文件：Services/ApiClient.cs
// 命名空间：IotGateway.WPF.Services
// 用途：API 客户端实现
//       使用 HttpClient 向后端 WebAPI 发送 HTTP 请求
//       自动携带 JWT Token（请求头 Authorization: Bearer xxx）
// ============================================================

using System.Net.Http.Headers;                   // 引入 AuthenticationHeaderValue
using System.Net.Http.Json;                      // 引入 ReadFromJsonAsync / PostAsJsonAsync 扩展方法
using System.Text;                               // 引入 Encoding
using System.Text.Json;                          // 引入 JSON 序列化
using IotGateway.WPF.Helpers;                    // 引入 TokenStorage
using IotGateway.WPF.Models;                     // 引入数据模型

namespace IotGateway.WPF.Services;

/// <summary>
/// API 客户端实现
/// 封装 HttpClient，提供统一的 API 调用方法
/// </summary>
public class ApiClient : IApiClient, IDisposable  // 实现 IApiClient 接口和 IDisposable
{
    // ============================================================
    // 私有字段
    // ============================================================

    /// <summary>HttpClient 实例（单例，全局复用，避免 Socket 耗尽）</summary>
    private readonly HttpClient _httpClient;

    /// <summary>JSON 序列化选项（统一配置）</summary>
    private readonly JsonSerializerOptions _jsonOptions;

    // ============================================================
    // 构造函数
    // ============================================================

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="baseUrl">后端 API 的基础地址，如 "http://localhost:5000"</param>
    public ApiClient(string baseUrl = "http://localhost:5000")
    {
        // 1. 创建 HttpClient 实例
        _httpClient = new HttpClient
        {
            // 设置基础地址：后续所有请求只需写相对路径
            // 如：_httpClient.PostAsync("/api/auth/login", content)
            // 实际请求地址 = http://localhost:5000/api/auth/login
            BaseAddress = new Uri(baseUrl),

            // 设置超时时间：30 秒无响应则取消请求
            Timeout = TimeSpan.FromSeconds(30)
        };

        // 2. 设置默认请求头
        //    告诉服务器：客户端接受 JSON 格式的响应
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        // 3. JSON 序列化配置
        _jsonOptions = new JsonSerializerOptions
        {
            // 属性名不区分大小写（C# 属性名首字母大写，JSON 通常小写）
            PropertyNameCaseInsensitive = true,

            // JSON 中允许 C# 类没有定义的额外字段（忽略它们，不报错）
            // 后端可能返回额外字段，前端不需要的忽略即可
            UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip
        };
    }

    // ============================================================
    // 令牌管理
    // ============================================================

    /// <summary>
    /// 设置 JWT Token（登录成功后调用）
    /// 之后所有请求都会自动在 Header 中携带此 Token
    /// </summary>
    /// <param name="token">JWT Token 字符串</param>
    public void SetToken(string token)
    {
        // 设置 Authorization 请求头
        // 格式：Bearer eyJhbGciOiJIUzI1NiIs...
        // 清除旧 Token（如果有），设置新 Token
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// 清除 JWT Token（退出登录时调用）
    /// </summary>
    public void ClearToken()
    {
        // 移除 Authorization 请求头
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    // ============================================================
    // API 方法实现
    // ============================================================

    /// <summary>
    /// 用户登录
    /// POST /api/auth/login
    /// </summary>
    public async Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request)
    {
        // 方法一：使用 PostAsJsonAsync（简洁方式）
        // 自动将 request 对象序列化为 JSON，发送 POST 请求
        var httpResponse = await _httpClient.PostAsJsonAsync(
            "/api/auth/login",                   // 请求路径
            request,                             // 请求体（自动序列化为 JSON）
            _jsonOptions                         // JSON 序列化选项
        );

        // 方法二（等价于上面，手动控制每个步骤）：
        // var json = JsonSerializer.Serialize(request, _jsonOptions);
        // var content = new StringContent(json, Encoding.UTF8, "application/json");
        // var httpResponse = await _httpClient.PostAsync("/api/auth/login", content);

        // 从 HTTP 响应中读取 JSON 并反序列化为 ApiResponse<LoginResponse>
        var apiResponse = await httpResponse.Content
            .ReadFromJsonAsync<ApiResponse<LoginResponse>>(_jsonOptions);

        // 如果反序列化失败（返回了非预期的格式），返回错误
        return apiResponse ?? new ApiResponse<LoginResponse>
        {
            Code = 500,
            Message = "服务器返回了无效的响应格式"
        };
    }

    // ============================================================
    // 资源释放
    // ============================================================

    /// <summary>
    /// 释放 HttpClient 资源
    /// </summary>
    public void Dispose()
    {
        _httpClient?.Dispose();                  // 释放 HttpClient（关闭 Socket 连接）
        GC.SuppressFinalize(this);               // 通知 GC 无需调用析构函数
    }
}
