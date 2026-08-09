using Moq;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using DS.Tools.Core.Interfaces;
using DS.Tools.Module.Git.Models;
using DS.Tools.Module.Git.Services;
using DS.Tools.Module.Git.ViewModels;

namespace DS.Tools.Tests.UnitTests;

/// <summary>
/// GitLogViewModel 单元测试（Moq 桩服务，不触达真实 git/系统对话框）
/// </summary>
public sealed class GitLogViewModelTests
{
    private readonly Mock<IGitLogService> _gitLog = new();
    private readonly Mock<IGitSettingsService> _settings = new();
    private readonly Mock<IFolderPickerService> _folderPicker = new();
    private readonly Mock<IClipboardService> _clipboard = new();

    public GitLogViewModelTests()
    {
        // 默认：无持久化文件夹 → ctor 不触发自动加载
        _settings.Setup(s => s.Load()).Returns(new GitSettings());
        _clipboard.Setup(c => c.SetTextAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
    }

    private GitLogViewModel CreateViewModel()
        => new(_gitLog.Object, _settings.Object, _folderPicker.Object, _clipboard.Object, NullLogger<GitLogViewModel>.Instance);

    /// <summary>
    /// 轮询等待异步流程完成（fire-and-forget 场景用）
    /// </summary>
    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;
            await Task.Delay(25);
        }
        throw new TimeoutException("等待异步流程完成超时");
    }

    [Fact]
    public async Task Ctor_WithSavedFolderPath_PopulatesRepositoryPathAndAutoLoads()
    {
        // Arrange
        const string savedPath = @"D:\Code\Self\DS.Tools";
        _settings.Setup(s => s.Load()).Returns(new GitSettings { LastFolderPath = savedPath });
        _gitLog.Setup(g => g.IsGitRepositoryAsync(savedPath, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _gitLog.Setup(g => g.GetCurrentBranchAsync(savedPath, It.IsAny<CancellationToken>())).ReturnsAsync("main");
        _gitLog.Setup(g => g.GetLogAsync(savedPath, It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GitLogResult.Success([new GitLogEntry("abc1234", "A", "a@x.com", DateTimeOffset.Now, "s1")]));

        // Act
        var vm = CreateViewModel();
        await WaitUntilAsync(() => !vm.IsLoading && vm.LogCount == 1);

        // Assert
        vm.RepositoryPath.Should().Be(savedPath);
        vm.BranchName.Should().Be("main");
        vm.LogCount.Should().Be(1);
        vm.HasErrors.Should().BeFalse();
    }

    [Fact]
    public async Task Ctor_WithoutSavedFolder_DoesNotQueryGit()
    {
        // Act
        var vm = CreateViewModel();
        await Task.Delay(100);

        // Assert（未持久化文件夹时不触发任何 git 调用）
        _gitLog.Verify(g => g.IsGitRepositoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        vm.RepositoryPath.Should().BeNull();
        vm.IsEmptyState.Should().BeTrue();
    }

    [Fact]
    public async Task PickFolder_SelectsFolder_SavesSettingsAndLoadsLog()
    {
        // Arrange
        const string selected = @"D:\Code\Self\DS.Tools";
        _folderPicker.Setup(f => f.PickFolderAsync(It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(selected);
        _gitLog.Setup(g => g.IsGitRepositoryAsync(selected, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _gitLog.Setup(g => g.GetCurrentBranchAsync(selected, It.IsAny<CancellationToken>())).ReturnsAsync("main");
        _gitLog.Setup(g => g.GetLogAsync(selected, It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GitLogResult.Success([
                new GitLogEntry("a1", "A", "a@x.com", DateTimeOffset.Now, "first"),
                new GitLogEntry("b2", "B", "b@x.com", DateTimeOffset.Now, "second")
            ]));
        var vm = CreateViewModel();

        // Act
        await vm.PickFolderCommand.ExecuteAsync(null);

        // Assert
        vm.RepositoryPath.Should().Be(selected);
        vm.BranchName.Should().Be("main");
        vm.LogEntries.Should().HaveCount(2);
        vm.LogCount.Should().Be(2);
        vm.HasErrors.Should().BeFalse();
        vm.StatusMessage.Should().Contain("共 2 条提交");
        _settings.Verify(s => s.Save(It.Is<GitSettings>(x => x.LastFolderPath == selected)), Times.Once);
        // 建议起始位置传入当前路径 + 对话框标题由调用方提供
        _folderPicker.Verify(f => f.PickFolderAsync(null, "选择 Git 仓库文件夹"), Times.Once);
    }

    [Fact]
    public async Task PickFolder_Cancelled_KeepsStateAndDoesNotSave()
    {
        // Arrange
        _folderPicker.Setup(f => f.PickFolderAsync(It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync((string?)null);
        var vm = CreateViewModel();

        // Act
        await vm.PickFolderCommand.ExecuteAsync(null);

        // Assert
        vm.RepositoryPath.Should().BeNull();
        _settings.Verify(s => s.Save(It.IsAny<GitSettings>()), Times.Never);
        _gitLog.Verify(g => g.IsGitRepositoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PickFolder_NotARepo_ShowsError()
    {
        // Arrange
        const string selected = @"C:\not-a-repo";
        _folderPicker.Setup(f => f.PickFolderAsync(It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(selected);
        _gitLog.Setup(g => g.IsGitRepositoryAsync(selected, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var vm = CreateViewModel();

        // Act
        await vm.PickFolderCommand.ExecuteAsync(null);

        // Assert
        vm.HasErrors.Should().BeTrue();
        vm.ErrorMessage.Should().Contain("不是 Git 仓库");
        vm.BranchName.Should().BeNull();
        vm.IsEmptyState.Should().BeFalse();
    }

    [Fact]
    public async Task LoadLog_OnSuccess_UpdatesStatusAndCount()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.RepositoryPath = @"D:\repo";
        _gitLog.Setup(g => g.GetLogAsync(@"D:\repo", It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GitLogResult.Success([
                new GitLogEntry("a1", "A", "a@x.com", DateTimeOffset.Now, "first")
            ]));

        // Act
        await vm.LoadLogCommand.ExecuteAsync(null);

        // Assert
        vm.LogCount.Should().Be(1);
        vm.StatusMessage.Should().Be("✓ 共 1 条提交");
        vm.HasErrors.Should().BeFalse();
    }

    [Fact]
    public async Task LoadLog_OnFailure_SetsErrorMessage()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.RepositoryPath = @"D:\repo";
        _gitLog.Setup(g => g.GetLogAsync(@"D:\repo", It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GitLogResult.Failure("git 命令执行超时"));

        // Act
        await vm.LoadLogCommand.ExecuteAsync(null);

        // Assert
        vm.HasErrors.Should().BeTrue();
        vm.ErrorMessage.Should().Contain("git 命令执行超时");
        vm.LogEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadLog_WithDateRange_PassesLocalOffsetDates()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.RepositoryPath = @"D:\repo";
        var since = new DateTime(2026, 1, 1);
        var until = new DateTime(2026, 3, 1);
        vm.SinceDate = since;
        vm.UntilDate = until;

        _gitLog.Setup(g => g.GetLogAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GitLogResult.Success([]));

        // Act
        await vm.LoadLogCommand.ExecuteAsync(null);

        // Assert（本地时间经本地时区偏移转换后传给服务；
        // 结束日期按"含当天"处理：次日零点作为 --until 排他边界）
        _gitLog.Verify(g => g.GetLogAsync(
            @"D:\repo",
            new DateTimeOffset(since, TimeZoneInfo.Local.GetUtcOffset(since)),
            new DateTimeOffset(until.AddDays(1), TimeZoneInfo.Local.GetUtcOffset(until.AddDays(1))),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Ctor_SetsDefaultWeekDateRange()
    {
        // 默认时间范围应为本周一至本周日
        var vm = CreateViewModel();

        var today = DateTime.Today;
        var expectedMonday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var expectedSunday = expectedMonday.AddDays(6);

        vm.SinceDate.Should().Be(expectedMonday);
        vm.UntilDate.Should().Be(expectedSunday);
    }

    [Fact]
    public async Task CopyLog_WithEntries_CopiesFormattedTextToClipboard()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.RepositoryPath = @"D:\repo";
        _gitLog.Setup(g => g.GetLogAsync(@"D:\repo", It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GitLogResult.Success([
                new GitLogEntry("abc1234", "Test User", "test@example.com",
                    new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.FromHours(8)), "fix: bug"),
                new GitLogEntry("def5678", "小毛 邵", "xiaomao@example.com",
                    new DateTimeOffset(2026, 3, 2, 9, 30, 0, TimeSpan.FromHours(8)), "feat: new feature")
            ]));
        await vm.LoadLogCommand.ExecuteAsync(null);

        string? captured = null;
        _clipboard.Setup(c => c.SetTextAsync(It.IsAny<string>()))
            .Callback<string>(t => captured = t)
            .Returns(Task.CompletedTask);

        // Act
        await vm.CopyLogCommand.ExecuteAsync(null);

        // Assert（每条日志一行：hash | 作者 | 日期 | 主题）
        captured.Should().Contain("abc1234 | Test User | 2026-03-01 10:00 | fix: bug");
        captured.Should().Contain("def5678 | 小毛 邵 | 2026-03-02 09:30 | feat: new feature");
        captured.Should().Contain(Environment.NewLine);
    }

    [Fact]
    public async Task CopyLog_WhenNoEntries_DoesNotTouchClipboard()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        await vm.CopyLogCommand.ExecuteAsync(null);

        // Assert（无日志时复制应静默跳过）
        _clipboard.Verify(c => c.SetTextAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CopyLogCommand_CanExecute_RequiresEntries()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.CopyLogCommand.CanExecute(null).Should().BeFalse();

        // 加载成功后有条目 → 可复制
        vm.RepositoryPath = @"D:\repo";
        _gitLog.Setup(g => g.GetLogAsync(@"D:\repo", It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GitLogResult.Success([new GitLogEntry("a1", "A", "a@x.com", DateTimeOffset.Now, "s1")]));
        await vm.LoadLogCommand.ExecuteAsync(null);
        vm.CopyLogCommand.CanExecute(null).Should().BeTrue();

        // 加载失败清空条目 → 不可复制
        _gitLog.Setup(g => g.GetLogAsync(@"D:\repo", It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GitLogResult.Failure("失败"));
        await vm.LoadLogCommand.ExecuteAsync(null);
        vm.CopyLogCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void LoadLogCommand_CanExecute_RequiresRepositoryPath()
    {
        var vm = CreateViewModel();

        vm.LoadLogCommand.CanExecute(null).Should().BeFalse();
        vm.RepositoryPath = "   ";
        vm.LoadLogCommand.CanExecute(null).Should().BeFalse();
        vm.RepositoryPath = @"D:\repo";
        vm.LoadLogCommand.CanExecute(null).Should().BeTrue();
    }
}
