// ============================================================
// 文件：App.xaml.cs
// 命名空间：IotGateway.WPF
// 用途：应用入口类（Prism 启动方式）
//
// WPF Prism 应用启动流程：
//   1. App.Main() 启动
//   2. PrismApplication.OnStartup() → CreateShell()
//   3. RegisterTypes() 注册 DI 容器
//   4. InitializeShell() 显示主窗口
// ============================================================

using System.Windows;                           // 引入 Window
using IotGateway.WPF.Converters;                // 引入值转换器
using IotGateway.WPF.Services;                  // 引入 IApiClient, ApiClient
using IotGateway.WPF.ViewModels;                // 引入 LoginViewModel
using IotGateway.WPF.Views;                     // 引入 LoginView
using Prism.Ioc;                                // 引入 IContainerRegistry

namespace IotGateway.WPF;

/// <summary>
/// 应用入口类
/// 继承 PrismApplication（在 App.xaml 中通过 XAML 声明）
/// </summary>
public partial class App : Prism.Unity.PrismApplication
{
    // ============================================================
    // Prism 要求的抽象方法实现
    // ============================================================

    /// <summary>
    /// 创建应用主窗口（Shell）
    /// Prism 框架在启动时调用
    /// </summary>
    /// <returns>应用的主窗口</returns>
    protected override Window CreateShell()
    {
        // 从 DI 容器中解析 MainWindow
        // Container.Resolve<T>() 会自动注入 T 的构造函数依赖
        var shell = Container.Resolve<MainWindow>();
        return shell;                            // 返回给 Prism 显示
    }

    /// <summary>
    /// 注册类型到 DI 容器
    ///
    /// 这是 Prism 的 DI 配置入口
    /// 所有 View、ViewModel、Service 都在这里注册
    /// </summary>
    /// <param name="containerRegistry">容器注册接口</param>
    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // ===== 1. 注册服务（单例模式）=====
        // 整个应用生命周期内只有一个 HttpClient 实例
        containerRegistry.RegisterSingleton<IApiClient, ApiClient>();

        // ===== 2. 注册 ViewModel =====
        // 每次导航到登录页时创建新的 LoginViewModel
        containerRegistry.Register<LoginViewModel>();

        // ===== 3. 注册 View 用于导航 =====
        // "LoginView" 是导航键，LoginView 是目标视图
        // 后续使用 _regionManager.RequestNavigate("MainRegion", "LoginView") 导航
        containerRegistry.RegisterForNavigation<LoginView>("LoginView");

        // ===== 4. 注册主窗口（单例）=====
        containerRegistry.RegisterSingleton<MainWindow>();
    }

    // ============================================================
    // 可选重写
    // ============================================================

    /// <summary>
    /// Prism 初始化完成后调用
    /// 在这里可以注册全局资源（样式、转换器等）
    /// </summary>
    protected override void OnInitialized()
    {
        base.OnInitialized();                    // 调用基类

        // 注册值转换器到全局资源
        // 这些转换器可以在所有 XAML 中通过 {StaticResource Key} 引用
        var resources = Current.Resources;
        resources["BoolToVisibility"] = new BooleanToVisibilityConverter();
        resources["InverseBool"] = new InverseBooleanConverter();
    }
}
