using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DS.Tools.Core.DI;
using DS.Tools.Module.Base.DI;
using DS.Tools.Module.Base.Interfaces;
using DS.Tools.Module.Text;
using DS.Tools.ViewModels;
using DS.Tools.Views;

namespace DS.Tools.Tests.UITests;

/// <summary>
/// 临时诊断2：用真实 App（加载 App.axaml 全量资源/样式/字体）复现一级菜单状态
/// （与 HeadlessTestApp 测试类不可同进程共存，须单独 filter 运行）
/// </summary>
public class MenuDiagAppTests
{
    private static bool _initialized;
    private static readonly object InitLock = new();

    private static void EnsureAppInitialized()
    {
        lock (InitLock)
        {
            if (_initialized)
                return;

            if (Application.Current is null)
            {
                AppBuilder.Configure<DS.Tools.App>()
                    .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                    .SetupWithoutStarting();
            }

            _initialized = true;
        }
    }

    private static void RenderFrame()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

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
        sp.GetRequiredService<IToolRegistry>().Register(module);
        return sp;
    }

    [Fact]
    public void Diag_RealApp_FirstLevelMenu()
    {
        EnsureAppInitialized();
        var sp = BuildContainer();

        Dispatcher.UIThread.Invoke(() =>
        {
            var window = sp.GetRequiredService<MainWindow>();
            window.DataContext = sp.GetRequiredService<MainWindowViewModel>();
            window.Show();
            RenderFrame();

            var texts = window.GetVisualDescendants().OfType<TextBlock>()
                .Where(t => t.Text is not null)
                .Select(t => $"'{t.Text}' vis={t.IsVisible} h={t.Bounds.Height}")
                .ToList();
            var expanders = window.GetVisualDescendants().OfType<Expander>().ToList();
            var moduleBorders = window.GetVisualDescendants().OfType<Border>()
                .Where(b => b.Classes.Contains("moduleItem"))
                .ToList();

            throw new Xunit.Sdk.XunitException(
                $"TEXTS[{texts.Count}]: {string.Join(" | ", texts.Take(24))}\n" +
                $"EXPANDERS[{expanders.Count}]: {string.Join(" | ", expanders.Select(e => $"vis={e.IsVisible} h={e.Bounds.Height}"))}\n" +
                $"moduleItem BORDERS[{moduleBorders.Count}]: {string.Join(" | ", moduleBorders.Select(b => $"vis={b.IsVisible} h={b.Bounds.Height}"))}");
        });
    }
}
