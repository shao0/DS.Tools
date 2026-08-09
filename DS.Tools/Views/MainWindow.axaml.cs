using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DS.Tools.Module.Base.Interfaces;
using DS.Tools.Module.Base.Services;

namespace DS.Tools.Views;

/// <summary>
/// 主窗口 - 应用程序主界面。
/// ViewModel → View 渲染经 IViewRegistry 注册表解析（AOT 兼容，零反射）——
/// 构造时挂载 <see cref="ViewRegistryDataTemplate"/> 到窗口 DataTemplates，
/// 替代逐条手写的编译期 DataTemplate 列表（映射在模块 Register 内经 ViewMappingRegistry 声明）。
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// 无参构造函数 —— 仅供 Avalonia XAML 编译器实例化要求（AVLN3000）；
    /// 运行时一律经 DI 解析 <see cref="MainWindow(IViewRegistry)"/>。
    /// </summary>
    public MainWindow() : this(null)
    {
    }

    /// <summary>
    /// 构造函数（经 DI 解析 IViewRegistry；须在主窗口创建前完成全部 View 注册）
    /// </summary>
    public MainWindow(IViewRegistry? viewRegistry)
    {
        InitializeComponent();

        // ViewModel → View 映射统一由注册表管理：模板匹配已注册 VM 时经编译期工厂创建 View
        if (viewRegistry is not null)
        {
            DataTemplates.Add(new ViewRegistryDataTemplate(viewRegistry));
        }
    }
}
