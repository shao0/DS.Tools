using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using DS.Tools.Module.Base.Interfaces;
using DS.Tools.Module.Base.Services;
using DS.Tools.Module.Git;
using DS.Tools.Module.Text;
using DS.Tools.ViewModels;
using DS.Tools.Views;

namespace DS.Tools.Tests.UITests;

/// <summary>
/// 主页（Dashboard）测试 - 按模块分组展示全部功能 + 导航命令 + 视图渲染。
/// 与其他 Headless UI 测试同集合：Avalonia 平台仅可初始化一次，须串行执行。
/// </summary>
[Collection("HeadlessUi")]
public class DashboardLauncherTests
{
    private static readonly object InitLock = new();
    private static bool _initialized;

    /// <summary>
    /// 初始化 Headless 平台（线程安全，整个测试程序集只初始化一次）
    /// </summary>
    private static void EnsureHeadlessInitialized()
    {
        lock (InitLock)
        {
            if (_initialized)
                return;

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
    /// 用真实模块构建主页 VM（与生产同构：模块 Register 入容器 + 真实 ToolRegistry 注册挂载子工具目录）
    /// </summary>
    private static (DashboardViewModel Vm, Mock<INavigationService> Nav) CreateViewModel()
    {
        var services = new ServiceCollection();

        var textModule = new TextModule();
        textModule.Register(services);
        services.AddSingleton(textModule);

        var gitModule = new GitModule();
        gitModule.Register(services);
        services.AddSingleton(gitModule);

        services.AddSingleton<IToolCatalog, ToolCatalog>();
        services.AddSingleton<IToolRegistry, ToolRegistry>();

        var sp = services.BuildServiceProvider();

        // ToolRegistry.Register 挂载子工具目录到模块基类（SubToolInfo 在 Register 阶段已入容器）
        var registry = sp.GetRequiredService<IToolRegistry>();
        registry.Register(textModule);
        registry.Register(gitModule);

        var nav = new Mock<INavigationService>();
        var vm = new DashboardViewModel(registry, nav.Object, NullLogger<DashboardViewModel>.Instance);
        return (vm, nav);
    }

    [Fact]
    public void DashboardViewModel_WithRealModules_BuildsGroupedTools()
    {
        EnsureHeadlessInitialized();

        Dispatcher.UIThread.Invoke(() =>
        {
            var (vm, _) = CreateViewModel();

            // 两个模块 → 两个分组
            vm.ModuleGroups.Should().HaveCount(2);
            vm.ToolCount.Should().Be(7); // 文本 6 个子工具 + Git 1 个

            var textGroup = vm.ModuleGroups.First(g => g.ModuleName == "文本工具");
            textGroup.ModuleIcon.Should().Be("📝");
            textGroup.Tools.Should().HaveCount(6);
            textGroup.Tools.Should().Contain(t => t.NavigationId == "text-tools:json-formatter");
            textGroup.Tools.Should().Contain(t => t.NavigationId == "text-tools:base64-converter");
            // 主页仪表盘已移出文本模块
            textGroup.Tools.Should().NotContain(t => t.NavigationId == "text-tools:dashboard");

            var gitGroup = vm.ModuleGroups.First(g => g.ModuleName == "Git 工具");
            gitGroup.ModuleIcon.Should().Be("🐙");
            gitGroup.Tools.Should().HaveCount(1);
            gitGroup.Tools[0].NavigationId.Should().Be("git-tools:git-log");
        });
    }

    [Fact]
    public void DashboardViewModel_NavigateToTool_InvokesNavigationService()
    {
        EnsureHeadlessInitialized();

        Dispatcher.UIThread.Invoke(() =>
        {
            var (vm, nav) = CreateViewModel();

            vm.NavigateToToolCommand.Execute("git-tools:git-log");
            nav.Verify(x => x.NavigateTo("git-tools:git-log"), Times.Once);

            // 空/空白导航 ID 不触发导航
            vm.NavigateToToolCommand.Execute(null);
            vm.NavigateToToolCommand.Execute("   ");
            nav.Verify(x => x.NavigateTo(It.IsAny<string>()), Times.Once);
        });
    }

    [Fact]
    public void DashboardView_TileButton_CommandIsBoundAndNavigates()
    {
        // 回归：磁贴命令曾绑定到 $parent[Window]——但主页视图渲染在 MainWindow 内容区，
        // $parent[Window] 解析到 MainWindow（DataContext=MainWindowViewModel），强转 DashboardViewModel 失败
        // → Command 为 null，磁贴不可点击。修复：$parent[UserControl]（最近的 UserControl 祖先即视图自身）。
        EnsureHeadlessInitialized();

        Dispatcher.UIThread.Invoke(() =>
        {
            var (vm, nav) = CreateViewModel();
            var window = new Window
            {
                Width = 900,
                Height = 700,
                Content = new DashboardView { DataContext = vm }
            };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();

            var tiles = window.GetVisualDescendants().OfType<Button>()
                .Where(b => b.CommandParameter is string)
                .ToList();
            tiles.Should().NotBeEmpty("主页应渲染出功能磁贴按钮");

            var tile = tiles.First(t => (string)t.CommandParameter! == "git-tools:git-log");
            tile.Command.Should().NotBeNull("磁贴按钮命令应经 $parent[UserControl] 绑定到 DashboardViewModel.NavigateToToolCommand");

            tile.Command!.Execute(tile.CommandParameter);
            nav.Verify(x => x.NavigateTo("git-tools:git-log"), Times.Once);

            window.Close();
        });
    }
}
