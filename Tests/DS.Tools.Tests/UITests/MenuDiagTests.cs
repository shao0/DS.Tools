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
/// 临时诊断：一级菜单视觉树结构检查（定位"一级菜单不显示"，确认后删除）
/// </summary>
[Collection("HeadlessUi")]
public class MenuDiagTests
{
    private static bool _initialized;
    private static readonly object InitLock = new();

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
                app.Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
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
    public void Diag_FirstLevelMenu_Structure()
    {
        EnsureHeadlessInitialized();
        var sp = BuildContainer();

        Dispatcher.UIThread.Invoke(() =>
        {
            var window = sp.GetRequiredService<MainWindow>();
            window.DataContext = sp.GetRequiredService<MainWindowViewModel>();
            window.Show();
            RenderFrame();

            var texts = window.GetVisualDescendants().OfType<TextBlock>()
                .Select(t => $"'{t.Text}' vis={t.IsVisible}")
                .ToList();
            var expanders = window.GetVisualDescendants().OfType<Expander>().ToList();
            var moduleBorders = window.GetVisualDescendants().OfType<Border>()
                .Where(b => b.Classes.Contains("moduleItem"))
                .ToList();

            var expander = expanders.FirstOrDefault();
            var inner = expander is null
                ? []
                : expander.GetVisualDescendants()
                    .Select(c => $"{c.GetType().Name} [{c.Bounds}] vis={c.IsVisible}")
                    .ToList();

            throw new Xunit.Sdk.XunitException(
                $"TEXTS[{texts.Count}]: {string.Join(" | ", texts)}\n" +
                $"EXPANDERS[{expanders.Count}]: {string.Join(" | ", expanders.Select(e => $"vis={e.IsVisible} b={e.Bounds}"))}\n" +
                $"moduleItem BORDERS[{moduleBorders.Count}]: {string.Join(" | ", moduleBorders.Select(b => $"vis={b.IsVisible} b={b.Bounds}"))}\n" +
                $"EXPANDER-INNER[{inner.Count}]: {string.Join(" | ", inner.Take(14))}");
        });
    }
}
