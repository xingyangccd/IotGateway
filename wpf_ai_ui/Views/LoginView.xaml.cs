// ============================================================
// 文件：Views/LoginView.xaml.cs
// 命名空间：IotGateway.WPF.Views
// 用途：登录页面的代码后置（Code-Behind）
//
// MVVM 原则：View 的代码后置应该尽量薄
// 只处理纯 UI 逻辑（如密码框绑定），业务逻辑全在 ViewModel 中
// ============================================================

using System.Windows.Controls;                  // 引入 UserControl 基类
using IotGateway.WPF.ViewModels;                // 引入 LoginViewModel

namespace IotGateway.WPF.Views;

/// <summary>
/// 登录页面 View
/// 继承 UserControl（用户控件，可嵌入其他容器中）
/// </summary>
public partial class LoginView : UserControl
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public LoginView()
    {
        InitializeComponent();                  // 加载 XAML 定义的 UI 元素
        // 注意：ViewModel 通过 DI 容器注入，在 Bootstrapper 中注册
        // this.DataContext 由 Prism 自动设置
    }

    /// <summary>
    /// 密码框的 PasswordChanged 事件处理
    ///
    /// 为什么需要这个？
    ///   WPF 的 PasswordBox.Password 属性不是依赖属性
    ///   不支持直接 Binding（出于安全考虑：防止密码以明文留在内存中）
    ///   所以需要在代码后置中手动同步密码值到 ViewModel
    /// </summary>
    private void PasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        // 将 PasswordBox 的密码同步到 ViewModel
        if (DataContext is LoginViewModel viewModel)  // 判断 DataContext 是否为 LoginViewModel
        {
            // PasswordBox.Password → ViewModel.Password
            viewModel.Password = PasswordBox.Password;
        }
    }
}
