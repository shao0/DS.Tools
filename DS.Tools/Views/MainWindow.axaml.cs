using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using DS.Tools.Module.Base.Interfaces;
using DS.Tools.Module.Base.Services;

namespace DS.Tools.Views;

/// <summary>
/// 主窗口 - 应用程序主界面。
/// ViewModel → View 渲染经 IToolCatalog 统一目录解析（AOT 兼容，零反射）——
/// 构造时挂载 <see cref="ViewRegistryDataTemplate"/> 到窗口 DataTemplates，
/// 替代逐条手写的编译期 DataTemplate 列表（映射在模块 Register 内经 ToolRegistration 声明）。
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// 无参构造函数 —— 仅供 Avalonia XAML 编译器实例化要求（AVLN3000）；
    /// 运行时一律经 DI 解析 <see cref="MainWindow(IToolCatalog)"/>。
    /// </summary>
    public MainWindow() : this(null)
    {
    }

    /// <summary>
    /// 构造函数（经 DI 解析 IToolCatalog；须在主窗口创建前完成全部注册）
    /// </summary>
    public MainWindow(IToolCatalog? catalog)
    {
        InitializeComponent();

        // ViewModel → View 映射统一由目录管理：模板匹配已注册 VM 时经 IoC 工厂创建 View
        if (catalog is not null)
        {
            DataTemplates.Add(new ViewRegistryDataTemplate(catalog));
        }
    }

    /// <summary>
    /// 监听窗口状态变化，同步切换 最大化/还原 图标（Avalonia 12 已无 WindowStateChanged 事件）。
    /// </summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WindowStateProperty)
        {
            UpdateWindowStateIcons();
        }
    }

    /// <summary>
    /// 标题栏按下：左键拖拽移动窗口，双击切换最大化/还原。
    /// 按钮区域（主页/切换/窗口控制）不参与拖拽——XAML 中仅标题栏空白区挂此处理器。
    /// </summary>
    private void TitleBar_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
        {
            return;
        }

        // 命中按钮及其子元素（Path/TextBlock）时不拖拽、不触发双击最大化
        if (e.Source is Visual source && source.FindAncestorOfType<Button>() is not null)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        BeginMoveDrag(e);
    }

    private void MinimizeButton_OnClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeButton_OnClick(object? sender, RoutedEventArgs e) => ToggleMaximize();

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void UpdateWindowStateIcons()
    {
        // OnPropertyChanged 可能在 InitializeComponent 完成前触发（x:Name 字段尚未赋值）
        if (MaximizeIcon is null || RestoreIcon is null)
        {
            return;
        }

        var maximized = WindowState == WindowState.Maximized;
        MaximizeIcon.IsVisible = !maximized;
        RestoreIcon.IsVisible = maximized;
    }
}
