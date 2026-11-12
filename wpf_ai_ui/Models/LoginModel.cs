// ============================================================
// 文件：Models/LoginModel.cs
// 命名空间：IotGateway.WPF.Models
// 用途：登录相关的请求/响应数据模型
// ============================================================

using System.ComponentModel;                    // 引入 INotifyPropertyChanged 接口
using System.Runtime.CompilerServices;           // 引入 CallerMemberName 特性

namespace IotGateway.WPF.Models;

/// <summary>
/// 登录请求模型
/// 发送给后端 API 的登录数据
/// </summary>
public class LoginRequest
{
    /// <summary>用户名</summary>
    public string Username { get; set; } = string.Empty;  // string.Empty 防止 null 引用

    /// <summary>密码（明文，通过 HTTPS 传输）</summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// 登录响应模型
/// 后端 API 返回的登录结果
/// </summary>
public class LoginResponse
{
    /// <summary>JWT Token 字符串</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Token 过期时间（Unix 时间戳）</summary>
    public long ExpiresAt { get; set; }

    /// <summary>用户信息</summary>
    public UserInfo? User { get; set; }
}

/// <summary>
/// 用户信息模型
/// 登录成功后返回的用户基本信息
/// </summary>
public class UserInfo
{
    /// <summary>用户ID</summary>
    public long Id { get; set; }

    /// <summary>用户名</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>真实姓名</summary>
    public string RealName { get; set; } = string.Empty;

    /// <summary>角色：Admin / Engineer / Operator</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>邮箱</summary>
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// API 统一响应模型（泛型）
/// 后端所有 API 返回的统一格式
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public class ApiResponse<T>
{
    /// <summary>状态码：200=成功, 400=请求错误, 401=未授权, 500=服务器错误</summary>
    public int Code { get; set; }

    /// <summary>响应消息</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>响应数据（泛型，不同接口返回不同类型）</summary>
    public T? Data { get; set; }

    /// <summary>是否成功（Code == 200）</summary>
    public bool IsSuccess => Code == 200;
}

/// <summary>
/// ViewModel 基类
/// 实现 INotifyPropertyChanged，所有 ViewModel 继承此类
/// Prism 的 BindableBase 也实现了类似功能，这里展示手动实现以理解原理
/// </summary>
public class ViewModelBase : INotifyPropertyChanged
{
    /// <summary>
    /// 属性变更通知事件
    /// 当 ViewModel 的属性值发生变化时，通知 View 更新 UI
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 触发属性变更通知
    /// </summary>
    /// <param name="propertyName">属性名称（由编译器自动填充，无需手动传）</param>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        // ？. 表示：如果 PropertyChanged 不为 null，则调用 Invoke 方法
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// 设置属性值并自动触发变更通知
    /// </summary>
    /// <typeparam name="T">属性类型</typeparam>
    /// <param name="field">私有字段引用（ref 关键字，直接修改原字段）</param>
    /// <param name="value">新值</param>
    /// <param name="propertyName">属性名称（自动填充）</param>
    /// <returns>值是否发生了变化</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        // 如果新旧值相同，不触发更新（性能优化）
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;                           // 更新私有字段
        OnPropertyChanged(propertyName);         // 通知 UI 属性已变更
        return true;
    }
}
