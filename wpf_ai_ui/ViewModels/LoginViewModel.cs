// ============================================================
// 文件：ViewModels/LoginViewModel.cs
// 命名空间：IotGateway.WPF.ViewModels
// 用途：登录页面的 ViewModel
//       处理登录逻辑、表单验证、调用 API、错误提示
//       MVVM 模式中，ViewModel 是 View 和 Model 之间的桥梁
// ============================================================

using System.Windows;                           // 引入 MessageBox
using System.Windows.Input;                      // 引入 ICommand 接口
using IotGateway.WPF.Helpers;                    // 引入 TokenStorage
using IotGateway.WPF.Models;                     // 引入 ViewModelBase, LoginRequest
using IotGateway.WPF.Services;                   // 引入 IApiClient
using Prism.Commands;                            // 引入 Prism 的 DelegateCommand

namespace IotGateway.WPF.ViewModels;

/// <summary>
/// 登录页面 ViewModel
/// 继承 ViewModelBase，获得属性变更通知能力
/// </summary>
public class LoginViewModel : ViewModelBase
{
    // ============================================================
    // 私有字段（对应属性，用于数据绑定）
    // ============================================================
    private readonly IApiClient _apiClient;      // API 客户端（通过构造函数注入）
    private string _username = string.Empty;     // 用户名输入框绑定值
    private string _password = string.Empty;     // 密码输入框绑定值
    private string _errorMessage = string.Empty; // 错误提示信息绑定值
    private bool _isLoading;                     // 是否正在加载中（显示等待动画/禁用按钮用）
    private string _statusText = string.Empty;   // 状态栏文本

    // ============================================================
    // 构造函数
    // ============================================================

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="apiClient">API 客户端（通过 DI 容器自动注入）</param>
    public LoginViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;                  // 保存 API 客户端引用

        // 初始化命令（将按钮点击绑定到对应方法）
        LoginCommand = new DelegateCommand(      // Prism 的 DelegateCommand
            ExecuteLogin,                        // 执行登录的方法
            CanExecuteLogin                      // 判断是否可以登录的方法
        );

        // 初始化时检查是否有已保存的 Token
        CheckSavedToken();
    }

    // ============================================================
    // 公共属性（与 View 绑定，View 通过 Binding 读取/设置这些属性）
    // ============================================================

    /// <summary>
    /// 用户名
    /// 绑定到 XAML 的 TextBox.Text="{Binding Username}"
    /// </summary>
    public string Username
    {
        get => _username;                        // 读取私有字段
        set
        {
            // SetProperty 会：1.更新字段值 2.通知 UI 刷新 3.触发 CanExecuteLogin 重新评估
            if (SetProperty(ref _username, value))
            {
                // 用户名变化时清除之前的错误提示
                ErrorMessage = string.Empty;
                // 通知 LoginCommand：条件变了，重新判断按钮是否可点击
                ((DelegateCommand)LoginCommand).RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// 密码
    /// 绑定到 XAML 的 PasswordBox（通过附加属性）
    /// </summary>
    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
            {
                ErrorMessage = string.Empty;
                ((DelegateCommand)LoginCommand).RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// 错误提示信息
    /// 绑定到 XAML 的 TextBlock.Text="{Binding ErrorMessage}"
    /// 显示红色错误文字
    /// </summary>
    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>
    /// 是否正在加载
    /// 绑定到 XAML 的 ProgressBar.Visibility 和 Button.IsEnabled
    /// true = 显示加载动画，禁用按钮
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    /// <summary>
    /// 状态栏文本
    /// 显示当前连接的后端地址
    /// </summary>
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    // ============================================================
    // 命令（按钮绑定的操作方法）
    // ============================================================

    /// <summary>
    /// 登录按钮命令
    /// 绑定到 XAML 的 Button.Command="{Binding LoginCommand}"
    /// </summary>
    public ICommand LoginCommand { get; }

    // ============================================================
    // 私有方法 - 登录逻辑
    // ============================================================

    /// <summary>
    /// 判断是否可以执行登录
    /// 条件：用户名和密码都不为空，且当前不在加载中
    /// </summary>
    private bool CanExecuteLogin()
    {
        return !string.IsNullOrWhiteSpace(Username)  // 用户名不为空
            && !string.IsNullOrWhiteSpace(Password)  // 密码不为空
            && !IsLoading;                           // 不在加载中
    }

    /// <summary>
    /// 执行登录操作
    /// 流程：参数校验 → 调用 API → 保存 Token → 通知主窗口切换页面
    /// </summary>
    private async void ExecuteLogin()
    {
        // ===== 第 1 步：参数校验 =====
        // 去除首尾空格
        var username = Username.Trim();
        var password = Password.Trim();

        // 空值检查（虽然按钮已禁用，但防御性编程）
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ErrorMessage = "请输入用户名和密码";
            return;                              // 提前返回，不继续执行
        }

        // ===== 第 2 步：开始加载 =====
        IsLoading = true;                        // 显示加载动画，禁用按钮
        ErrorMessage = string.Empty;             // 清除之前的错误信息
        StatusText = "正在连接服务器...";

        try
        {
            // ===== 第 3 步：调用登录 API =====
            // 构建请求对象
            var loginRequest = new LoginRequest
            {
                Username = username,             // 用户名
                Password = password              // 密码（生产环境应通过 HTTPS 传输）
            };

            // 异步调用 API（不阻塞 UI 线程）
            StatusText = "正在验证身份...";
            var response = await _apiClient.LoginAsync(loginRequest);

            // ===== 第 4 步：处理响应 =====
            if (response.IsSuccess && response.Data != null)
            {
                // --- 登录成功 ---
                // 4.1 保存 Token 到 ApiClient（后续请求自动携带）
                _apiClient.SetToken(response.Data.Token);

                // 4.2 保存 Token 到本地文件（应用重启后无需重新登录）
                var expiresAt = DateTimeOffset
                    .FromUnixTimeSeconds(response.Data.ExpiresAt)  // Unix 时间戳 → DateTime
                    .DateTime;
                TokenStorage.SaveToken(response.Data.Token, expiresAt);

                // 4.3 更新状态
                StatusText = $"登录成功！欢迎 {response.Data.User?.RealName}";
                ErrorMessage = string.Empty;     // 清除错误

                // 4.4 通知登录成功（通过事件或消息）
                //     这里使用简单的 MessageBox，后续会换成 Prism EventAggregator
                OnLoginSuccess?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                // --- 登录失败 ---
                ErrorMessage = response.Message; // 显示后端返回的错误消息
                StatusText = "登录失败";
            }
        }
        catch (HttpRequestException ex)
        {
            // --- 网络异常 ---
            // HttpRequestException：无法连接服务器、DNS 解析失败等
            ErrorMessage = $"无法连接到服务器，请检查网络和服务器地址\n详情：{ex.Message}";
            StatusText = "连接失败";
        }
        catch (TaskCanceledException)
        {
            // --- 超时 ---
            ErrorMessage = "请求超时，服务器响应过慢，请稍后重试";
            StatusText = "请求超时";
        }
        catch (Exception ex)
        {
            // --- 其他未预期的异常 ---
            ErrorMessage = $"发生未知错误：{ex.Message}";
            StatusText = "发生异常";
        }
        finally
        {
            // ===== 第 5 步：结束加载（无论成功还是失败都会执行） =====
            IsLoading = false;                   // 隐藏加载动画，启用按钮
        }
    }

    /// <summary>
    /// 检查是否有已保存的有效 Token
    /// 有则跳过登录页，直接进入主页面
    /// </summary>
    private void CheckSavedToken()
    {
        if (TokenStorage.IsTokenValid())
        {
            // 获取已保存的 Token
            var tokenInfo = TokenStorage.GetToken();
            if (tokenInfo != null)
            {
                // 设置到 ApiClient（后续请求自动携带）
                _apiClient.SetToken(tokenInfo.Token);
                StatusText = "检测到已保存的登录状态，正在自动登录...";

                // 触发自动登录成功事件
                // 注意：这里没有验证 Token 是否在后端仍然有效
                // 如果后端已使此 Token 失效，后续 API 调用会返回 401
                OnLoginSuccess?.Invoke(this, EventArgs.Empty);
            }
        }
        else
        {
            StatusText = "请登录以继续";
        }
    }

    // ============================================================
    // 事件
    // ============================================================

    /// <summary>
    /// 登录成功事件
    /// MainWindow 订阅此事件，登录成功后切换到主页面
    /// 后续会用 Prism EventAggregator 或 Navigation 替代
    /// </summary>
    public event EventHandler? OnLoginSuccess;
}
