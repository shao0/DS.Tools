using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using DS.Tools.Core.Interfaces;
using DS.Tools.Module.Text.Services;
using DS.Tools.Module.Text.ViewModels;
using DS.Tools.Module.Text.Views;
using DS.Tools.Views;

namespace DS.Tools.Tests.UITests;

/// <summary>
/// Headless 测试应用 - Avalonia 无头平台入口
/// </summary>
public sealed class HeadlessTestApp : Application;

/// <summary>
/// 视图渲染冒烟测试（Avalonia Headless 平台）-
/// 验证各工具 View 能无异常实例化并应用模板/样式（定位"界面不显示"类问题的回归）。
/// 与其他 Headless UI 测试同集合：Avalonia 平台仅可初始化一次，须串行执行。
/// </summary>
[Collection("HeadlessUi")]
public class ViewRenderTests
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

            _initialized = true;
        }
    }

    [Fact]
    public void JsonFormatterView_ShouldInstantiate()
    {
        EnsureHeadlessInitialized();

        Dispatcher.UIThread.Invoke(() =>
        {
            var vm = new JsonFormatterViewModel(
                new JsonFormatterService(),
                new ClipboardServiceStub(),
                NullLogger<JsonFormatterViewModel>.Instance);

            var view = new JsonFormatterView { DataContext = vm };
            view.ApplyTemplate();

            view.GetVisualChildren().Count().Should().BeGreaterThan(0, "视图应包含渲染后的子元素");
        });
    }

    [Fact]
    public void DashboardView_ShouldInstantiate()
    {
        EnsureHeadlessInitialized();

        Dispatcher.UIThread.Invoke(() =>
        {
            var view = new DashboardView();
            view.ApplyTemplate();
            view.GetVisualChildren().Count().Should().BeGreaterThan(0);
        });
    }

    [Fact]
    public void Base64View_ShouldInstantiate()
    {
        EnsureHeadlessInitialized();

        Dispatcher.UIThread.Invoke(() =>
        {
            var view = new Base64View();
            view.ApplyTemplate();
            view.GetVisualChildren().Count().Should().BeGreaterThan(0);
        });
    }

    /// <summary>
    /// 测试用剪贴板桩（不触达系统剪贴板）
    /// </summary>
    private sealed class ClipboardServiceStub : IClipboardService
    {
        public Task SetTextAsync(string text) => Task.CompletedTask;
        public Task<string?> GetTextAsync() => Task.FromResult<string?>(null);
    }
}
