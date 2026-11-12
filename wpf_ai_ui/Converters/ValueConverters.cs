// ============================================================
// 文件：Converters/ValueConverters.cs
// 命名空间：IotGateway.WPF.Converters
// 用途：WPF 值转换器集合
//
// 值转换器是 WPF 数据绑定的重要组成部分
// 作用：在 View 和 ViewModel 之间做数据格式转换
//
// 常见场景：
//   - bool → Visibility（true显示/false隐藏）
//   - bool → bool取反（IsLoading=true → IsEnabled=false）
//   - int → Color（数值范围对应的颜色）
// ============================================================

using System.Globalization;                     // 引入 CultureInfo
using System.Windows;                           // 引入 Visibility
using System.Windows.Data;                      // 引入 IValueConverter

namespace IotGateway.WPF.Converters;

/// <summary>
/// 布尔值 → 可见性 转换器
///
/// 使用场景：
///   TextBlock Visibility="{Binding ErrorMessage, Converter={StaticResource BoolToVisibility}}"
///   当 ErrorMessage 不为空时 → Visible
///   当 ErrorMessage 为空时 → Collapsed
///
/// Convert 方法：ViewModel 值 → View 显示值
/// ConvertBack 方法：View 显示值 → ViewModel 值（双向绑定用）
/// </summary>
public class BooleanToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// 将 ViewModel 的值转换为 View 可用的值
    /// </summary>
    /// <param name="value">ViewModel 绑定源的值</param>
    /// <param name="targetType">目标类型（这里是 Visibility）</param>
    /// <param name="parameter">转换参数（XAML 中 ConverterParameter 指定的值）</param>
    /// <param name="culture">当前区域信息</param>
    /// <returns>转换后的值</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // 判断逻辑：
        //   1. 如果是 bool 类型 → true=Visible, false=Collapsed
        //   2. 如果是 string 类型 → 不为空=Visible, 为空=Collapsed
        //   3. 如果是其他引用类型 → 不为null=Visible, null=Collapsed

        if (value is bool boolValue)
        {
            // bool 转换：true → 显示，false → 隐藏
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        if (value is string stringValue)
        {
            // string 转换：非空 → 显示，空 → 隐藏
            return !string.IsNullOrEmpty(stringValue)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // 引用类型：不为 null → 显示
        return value != null ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// 反向转换（Visibility → bool/string）
    /// 单向绑定不需要，直接返回原值
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();     // 本项目中不需要反向转换
    }
}

/// <summary>
/// 布尔值取反转换器
///
/// 使用场景：
///   登录按钮 IsEnabled="{Binding IsLoading, Converter={StaticResource InverseBool}}"
///   IsLoading=true → IsEnabled=false（加载中禁用按钮）
///   IsLoading=false → IsEnabled=true（空闲时启用按钮）
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    /// <summary>
    /// 将 bool 值取反
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;                   // 简单的逻辑取反
        }

        return value;                            // 非 bool 类型原样返回
    }

    /// <summary>
    /// 反向转换（取反的取反 = 原值）
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;                   // 取反的取反 = 原值
        }

        return value;
    }
}
