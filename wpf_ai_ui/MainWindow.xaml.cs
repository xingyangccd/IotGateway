// ============================================================
// 文件：MainWindow.xaml.cs
// 命名空间：IotGateway.WPF
// 用途：主窗口代码后置
//       负责页面切换逻辑（登录页 ↔ 主页面）
// ============================================================

using System.Windows;                           // 引入 Window 基类
using System.Windows.Controls;                   // 引入 ContentControl
using IotGateway.WPF.ViewModels;                // 引入 LoginViewModel
using IotGateway.WPF.Views;                     // 引入 LoginView

namespace IotGateway.WPF;

/// <summary>
/// 主窗口
/// 作为应用的 Shell，管理页面切换
/// </summary>
public partial class MainWindow : Window
{
    private readonly LoginViewModel _loginViewModel; // 登录 ViewModel 引用

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="loginViewModel">登录 ViewModel（通过 DI 注入）</param>
    public MainWindow(LoginViewModel loginViewModel)
    {
        InitializeComponent();                  // 加载 XAML UI

        _loginViewModel = loginViewModel;       // 保存引用

        // 订阅登录成功事件
        _loginViewModel.OnLoginSuccess += OnLoginSuccess;

        // 初始显示登录页面
        ShowLoginView();
    }

    /// <summary>
    /// 显示登录页面
    /// </summary>
    private void ShowLoginView()
    {
        // 创建 LoginView 实例并设置 DataContext
        var loginView = new LoginView
        {
            DataContext = _loginViewModel       // ViewModel 绑定到 View
        };

        // 将 ContentControl 的内容设置为登录页面
        MainRegion.Content = loginView;
    }

    /// <summary>
    /// 登录成功回调
    /// 切换显示主页面
    /// </summary>
    private void OnLoginSuccess(object? sender, EventArgs e)
    {
        // 取消订阅事件（防止内存泄漏）
        _loginViewModel.OnLoginSuccess -= OnLoginSuccess;

        // 在主线程（UI 线程）上更新界面
        Dispatcher.Invoke(() =>
        {
            // 显示主页面（目前显示一个简单的欢迎页面）
            // 后续章节会替换为完整的 Prism Region 导航
            var mainPage = new TextBlock
            {
                Text = $"登录成功！欢迎使用工业物联网管理系统",
                FontSize = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            MainRegion.Content = mainPage;

            // 将窗口标题改为包含用户名
            Title = $"工业物联网管理系统 - 欢迎";
        });
    }
}
