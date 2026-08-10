using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using DS.Tools.Core.Interfaces;
using DS.Tools.Module.Git.Models;
using DS.Tools.Module.Git.Services;
using DS.Tools.Module.Git.ViewModels;
using DS.Tools.Module.Git.Views;

namespace DS.Tools.Tests.UITests;

/// <summary>
/// Git 日志视图渲染冒烟测试（Avalonia Headless 平台）-
/// 验证 GitLogView 能无异常实例化并渲染绑定内容。
/// 与其他 Headless UI 测试同集合：Avalonia 平台仅可初始化一次，须串行执行。
/// </summary>
[Collection("HeadlessUi")]
public class GitLogViewRenderTests
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
    /// 强制处理 UI 线程作业并触发一帧渲染（Headless 无真实帧循环，需手动驱动）
    /// </summary>
    private static void RenderFrame()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// 构造带桩服务的 VM（无持久化设置 → 不触发自动加载，测试可控）
    /// </summary>
    private static GitLogViewModel CreateViewModel(StubClipboardService? clipboard = null)
        => new(
            new StubGitLogService(),
            new StubSettingsService(),
            new StubFolderPickerService(),
            clipboard ?? new StubClipboardService(),
            NullLogger<GitLogViewModel>.Instance);

    /// <summary>
    /// 在窗口内承载视图并渲染（绑定需控件挂载到视觉树后才生效）
    /// </summary>
    private static void RenderInWindow(GitLogViewModel vm, out Window window, out GitLogView view)
    {
        window = new Window
        {
            Width = 900,
            Height = 600,
            Content = view = new GitLogView { DataContext = vm }
        };
        window.Show();
        RenderFrame();
    }

    [Fact]
    public void GitLogView_ShouldInstantiate_WithEmptyState()
    {
        EnsureHeadlessInitialized();

        Dispatcher.UIThread.Invoke(() =>
        {
            RenderInWindow(CreateViewModel(), out var window, out var view);

            view.GetVisualChildren().Count().Should().BeGreaterThan(0, "视图应包含渲染后的子元素");
            // 空状态提示应显示
            window.GetVisualDescendants().OfType<TextBlock>()
                .Any(t => t.Text is not null && t.Text.Contains("尚未加载日志")).Should().BeTrue("空状态提示应显示");

            window.Close();
        });
    }

    [Fact]
    public void GitLogView_WithRepositories_ShouldRenderListItems()
    {
        EnsureHeadlessInitialized();

        Dispatcher.UIThread.Invoke(() =>
        {
            var vm = CreateViewModel();
            vm.RepositoryPath = @"D:\repo";
            vm.BranchName = "main";
            vm.Repositories =
            [
                new GitRepositoryLog("根仓库", [new GitLogEntry("abc1234", "Test User", "test@example.com",
                    new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.FromHours(8)), "fix: critical bug")], IsRoot: true)
            ];
            vm.SelectedRepository = vm.Repositories[0];

            RenderInWindow(vm, out var window, out _);
            RenderFrame();

            // 提交主题与分支信息应渲染进视觉树
            window.GetVisualDescendants().OfType<TextBlock>()
                .Any(t => t.Text == "fix: critical bug").Should().BeTrue("提交主题应显示在日志列表中");
            window.GetVisualDescendants().OfType<TextBlock>()
                .Any(t => t.Text == "abc1234").Should().BeTrue("提交哈希应显示在日志列表中");
            window.GetVisualDescendants().OfType<TextBlock>()
                .Any(t => t.Text == "main").Should().BeTrue("当前分支应显示在仓库信息卡中");
            // 条目复制按钮应渲染（仅复制该条）
            window.GetVisualDescendants().OfType<Button>()
                .Count(b => b.Content?.ToString() == "📋").Should().Be(1, "每条日志条目应有复制按钮");

            window.Close();
        });
    }

    [Fact]
    public void GitLogView_WithMultiLineMessages_ShouldRenderFullMessages()
    {
        // 回归：%B 完整消息（含正文与换行）应完整渲染——%s 仅首行主题是此前的显示缺陷
        EnsureHeadlessInitialized();

        Dispatcher.UIThread.Invoke(() =>
        {
            var vm = CreateViewModel();
            vm.RepositoryPath = @"D:\repo";
            vm.BranchName = "main";
            vm.Repositories =
            [
                new GitRepositoryLog("根仓库", [
                    new GitLogEntry("abc1234", "Test User", "test@example.com",
                        new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.FromHours(8)),
                        "feat: multi-line body\n\nline one\n\nline three"),
                    new GitLogEntry("def5678", "Test User", "test@example.com",
                        new DateTimeOffset(2026, 3, 2, 10, 0, 0, TimeSpan.FromHours(8)),
                        "fix: second commit")
                ], IsRoot: true)
            ];
            vm.SelectedRepository = vm.Repositories[0];

            RenderInWindow(vm, out var window, out _);
            RenderFrame();

            // 多行消息完整渲染（正文段落可见，而非仅首行主题）。
            // 显示层经 GitLogMessageConverter 压缩连续换行（规避 Avalonia 12.1.x Wrap 空段落布局死循环），
            // 复制仍走 VM 原始 Message（保留 \n\n 空段落），见复制测试断言。
            window.GetVisualDescendants().OfType<TextBlock>()
                .Any(t => t.Text == "feat: multi-line body\nline one\nline three").Should().BeTrue("多行提交消息应完整显示在日志列表中");
            // 多条记录全部渲染（非仅第一条）
            window.GetVisualDescendants().OfType<TextBlock>()
                .Any(t => t.Text == "fix: second commit").Should().BeTrue("第二条提交应显示在日志列表中");
            window.GetVisualDescendants().OfType<TextBlock>()
                .Any(t => t.Text == "def5678").Should().BeTrue("第二条提交的哈希应显示");

            window.Close();
        });
    }

    [Fact]
    public void GitLogView_WithError_ShouldRenderErrorPanel()
    {
        EnsureHeadlessInitialized();

        Dispatcher.UIThread.Invoke(() =>
        {
            var vm = CreateViewModel();
            vm.RepositoryPath = @"D:\not-a-repo";

            // 直接设置错误状态（等价于服务返回失败后的 VM 状态）
            vm.ErrorMessage = "所选文件夹不是 Git 仓库";
            vm.HasErrors = true;

            RenderInWindow(vm, out var window, out _);

            window.GetVisualDescendants().OfType<TextBlock>()
                .Any(t => t.Text == "所选文件夹不是 Git 仓库").Should().BeTrue("错误消息应显示在错误面板中");

            window.Close();
        });
    }

    /// <summary>
    /// 测试用文件夹选择器桩（不打开系统对话框）
    /// </summary>
    private sealed class StubFolderPickerService : IFolderPickerService
    {
        public Task<string?> PickFolderAsync(string? suggestedPath, string? title) => Task.FromResult<string?>(null);
    }

    /// <summary>
    /// 测试用 git 服务桩（不触达真实 git CLI）
    /// </summary>
    private sealed class StubGitLogService : IGitLogService
    {
        public Task<bool> IsGitRepositoryAsync(string path, CancellationToken ct = default) => Task.FromResult(true);

        public Task<string?> GetCurrentBranchAsync(string repoPath, CancellationToken ct = default)
            => Task.FromResult<string?>("main");

        public Task<GitLogResult> GetLogAsync(string repoPath, DateTimeOffset? since, DateTimeOffset? until, CancellationToken ct = default)
            => Task.FromResult(GitLogResult.Success([]));
    }

    /// <summary>
    /// 测试用设置服务桩（无持久化）
    /// </summary>
    private sealed class StubSettingsService : IGitSettingsService
    {
        public GitSettings Load() => new();

        public void Save(GitSettings settings)
        {
        }
    }

    /// <summary>
    /// 测试用剪贴板桩（不触达系统剪贴板，记录最近一次写入内容）
    /// </summary>
    private sealed class StubClipboardService : IClipboardService
    {
        public string? LastText { get; private set; }

        public Task SetTextAsync(string text)
        {
            LastText = text;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void GitLogView_CopyEntryButton_CommandIsBoundAndCopiesEntry()
    {
        // 回归：复制单条命令曾绑定到 $parent[Window]——视图渲染在 MainWindow 内容区，
        // $parent[Window] 解析到 MainWindow（DataContext=MainWindowViewModel），强转 GitLogViewModel 失败
        // → Command 为 null，按钮点击无反应。修复：$parent[UserControl]（最近的 UserControl 祖先即视图自身）。
        EnsureHeadlessInitialized();

        Dispatcher.UIThread.Invoke(() =>
        {
            var clipboard = new StubClipboardService();
            var vm = CreateViewModel(clipboard);
            vm.RepositoryPath = @"D:\repo";
            vm.BranchName = "main";
            vm.Repositories =
            [
                new GitRepositoryLog("根仓库", [new GitLogEntry("abc1234", "Test User", "test@example.com",
                    new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.FromHours(8)), "fix: critical bug")], IsRoot: true)
            ];
            vm.SelectedRepository = vm.Repositories[0];

            RenderInWindow(vm, out var window, out _);
            RenderFrame();

            var copyButton = window.GetVisualDescendants().OfType<Button>()
                .Single(b => b.Content?.ToString() == "📋");
            copyButton.Command.Should().NotBeNull("复制单条按钮命令应经 $parent[UserControl] 绑定到 GitLogViewModel.CopyEntryCommand");

            copyButton.Command!.Execute(copyButton.CommandParameter);
            clipboard.LastText.Should().Contain("fix: critical bug", "点击复制按钮应将该条完整消息写入剪贴板");

            window.Close();
        });
    }
}
