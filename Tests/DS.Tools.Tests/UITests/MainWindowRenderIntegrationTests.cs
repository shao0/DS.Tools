using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DS.Tools.Core.DI;
using DS.Tools.Module.Base.DI;
using DS.Tools.Module.Base.Interfaces;
using DS.Tools.Module.Text;
using DS.Tools.Module.Text.ViewModels;
using DS.Tools.ViewModels;
using DS.Tools.Views;

namespace DS.Tools.Tests.UITests;

/// <summary>
/// 主窗口端到端渲染集成测试（Avalonia Headless）-
/// 覆盖：侧边栏布局（版本固定底部、菜单完整）、子工具导航与 ViewModel→View 渲染。
/// 与其他 Headless UI 测试同集合：Avalonia 平台仅可初始化一次，须串行执行。
/// </summary>
[Collection("HeadlessUi")]
public class MainWindowRenderIntegrationTests
{
    private static readonly object InitLock = new();
    private static bool _initialized;
    private static readonly CollectingLogSink LogSink = new();

    /// <summary>
    /// Avalonia 日志捕获器（用于暴露绑定错误等诊断信息）
    /// </summary>
    private sealed class CollectingLogSink : Avalonia.Logging.ILogSink
    {
        public List<string> Messages { get; } = [];

        public bool IsEnabled(Avalonia.Logging.LogEventLevel level, string area) => true;

        public void Log(Avalonia.Logging.LogEventLevel level, string area, object? source, string messageTemplate)
            => Messages.Add($"[{level}] {area}: {messageTemplate}");

        public void Log(Avalonia.Logging.LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
            => Messages.Add($"[{level}] {area}: {string.Format(messageTemplate.Replace("{", "{{").Replace("}", "}}"), propertyValues)}");
    }

    private static void EnsureHeadlessInitialized()
    {
        lock (InitLock)
        {
            if (_initialized)
                return;

            Avalonia.Logging.Logger.Sink = LogSink;

            if (Application.Current is null)
            {
                AppBuilder.Configure<HeadlessTestApp>()
                    .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                    .SetupWithoutStarting();
            }

            var app = (HeadlessTestApp)Application.Current!;
            if (app.Styles.Count == 0)
            {
                app.Styles.Add(new FluentTheme());
            }

            _initialized = true;
        }
    }

    /// <summary>
    /// 强制处理 UI 线程作业并触发一帧渲染（Headless 无真实帧循环，需手动驱动）
    /// </summary>
    private static void RenderFrame()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// 构建与 App.axaml.cs 等价的组合根容器
    /// </summary>
    private static IServiceProvider BuildContainer()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddApplicationServices();
        services.AddModuleServices();

        var module = new TextModule();
        module.Register(services);
        services.AddSingleton(module);

        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<MainWindow>();

        var sp = services.BuildServiceProvider();

        // 等价于 App.InitializeToolModules：模块注册进 ToolRegistry
        sp.GetRequiredService<IToolRegistry>().Register(module);

        return sp;
    }

    [Fact]
    public void MainWindow_NavigateToJsonTool_ShouldRenderJsonFormatterView()
    {
        EnsureHeadlessInitialized();
        var sp = BuildContainer();

        Dispatcher.UIThread.Invoke(() =>
        {
            var window = sp.GetRequiredService<MainWindow>();
            window.DataContext = sp.GetRequiredService<MainWindowViewModel>();
            window.Show();
            RenderFrame();

            // 默认导航应为仪表盘
            var vm = ((MainWindowViewModel)window.DataContext!).ActiveToolViewModel;
            vm.Should().BeOfType<DashboardViewModel>();

            // 通过侧边栏菜单导航到 JSON 格式化
            var jsonText = window.GetVisualDescendants().OfType<TextBlock>()
                .First(t => t.Text == "JSON格式化");
            var jsonButton = jsonText.GetVisualAncestors().OfType<Button>().First();
            jsonButton.Command!.Execute(jsonButton.CommandParameter);
            RenderFrame();

            // 导航后内容区应切换为 JsonFormatterViewModel 并渲染对应 View
            var targetVm = ((MainWindowViewModel)window.DataContext!).ActiveToolViewModel;
            targetVm.Should().BeOfType<JsonFormatterViewModel>();

            var presenters = window.GetVisualDescendants().OfType<ContentPresenter>().ToList();
            var contentOwners = window.GetVisualDescendants().OfType<ContentControl>().ToList();
            var contentArea = contentOwners.Last();
            var bindingState = contentArea.DataContext is null
                ? "DataContext=null"
                : $"DataContext={contentArea.DataContext.GetType().Name}";

            var bindingErrors = LogSink.Messages
                .Where(m => m.Contains("Binding") || m.Contains("binding"))
                .ToList();

            RenderFrame();

            var presenter = presenters.FirstOrDefault(p => ReferenceEquals(p.Content, targetVm));
            presenter.Should().NotBeNull(
                $"JsonFormatterViewModel 应被 ContentPresenter 承载；ContentPresenter 共 {presenters.Count} 个，" +
                $"Content 类型: [{string.Join(", ", presenters.Select(p => p.Content?.GetType().Name ?? "null"))}]，" +
                $"ContentControl 共 {contentOwners.Count} 个，" +
                $"Content 类型: [{string.Join(", ", contentOwners.Select(c => c.Content?.GetType().Name ?? "null"))}]，" +
                $"内容区 {bindingState}；" +
                $"绑定日志: [{string.Join(" | ", bindingErrors)}]");
            presenter!.Child.Should().BeOfType<DS.Tools.Module.Text.Views.JsonFormatterView>();
        });
    }

    [Fact]
    public void MainWindow_VersionInfo_ShouldBeDockedAtSidebarBottom()
    {
        EnsureHeadlessInitialized();
        var sp = BuildContainer();

        Dispatcher.UIThread.Invoke(() =>
        {
            var window = sp.GetRequiredService<MainWindow>();
            window.DataContext = sp.GetRequiredService<MainWindowViewModel>();
            window.Show();
            RenderFrame();

            var versionText = window.GetVisualDescendants().OfType<TextBlock>()
                .First(t => t.Text == "DS.Tools v1.0");

            var sidebar = window.GetVisualDescendants().OfType<Border>()
                .First(b => b.Classes.Contains("sidebar"));

            // Bounds 为相对父元素坐标，需转换为窗口绝对坐标比较
            var versionBottom = versionText.TranslatePoint(new Point(0, versionText.Bounds.Height), window)!.Value.Y;
            var sidebarBottom = sidebar.TranslatePoint(new Point(0, sidebar.Bounds.Height), window)!.Value.Y;

            // 版本信息应贴靠侧边栏底部（距底距离 = 版本区 Padding 12 + 文字高，远小于侧边栏高度）
            (sidebarBottom - versionBottom).Should().BeLessThan(40, "版本信息应固定在侧边栏底部");
            versionBottom.Should().BeGreaterThan(sidebarBottom - 60, "版本信息应固定在侧边栏底部");

            // 侧边栏菜单区域应填满 Logo 与版本信息之间的全部空间
            var scrollViewer = window.GetVisualDescendants().OfType<ScrollViewer>()
                .First(s => s.Classes.Contains("none") || s.GetType().Name == "ScrollViewer");
            scrollViewer.Bounds.Height.Should().BeGreaterThan(300, "菜单区域应占据侧边栏主要空间");
        });
    }
}
