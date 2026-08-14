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
            .ReturnsAsync(GitLogResult.Success([new GitRepositoryLog("根仓库", [new GitLogEntry("abc1234", "A", "a@x.com", DateTimeOffset.Now, "s1")], IsRoot: true)]));

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
                new GitRepositoryLog("根仓库", [
                    new GitLogEntry("a1", "A", "a@x.com", DateTimeOffset.Now, "first"),
                    new GitLogEntry("b2", "B", "b@x.com", DateTimeOffset.Now, "second")
                ], IsRoot: true)
            ]));
        var vm = CreateViewModel();

        // Act
        await vm.PickFolderCommand.ExecuteAsync(null);

        // Assert
        vm.RepositoryPath.Should().Be(selected);
        vm.BranchName.Should().Be("main");
        vm.SelectedRepository!.Entries.Should().HaveCount(2);
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
                new GitRepositoryLog("根仓库", [new GitLogEntry("a1", "A", "a@x.com", DateTimeOffset.Now, "first")], IsRoot: true)
            ]));

        // Act
        await vm.LoadLogCommand.ExecuteAsync(null);

        // Assert
        vm.LogCount.Should().Be(1);
        vm.StatusMessage.Should().Be("✓ 共 1 条提交");
        vm.HasErrors.Should().BeFalse();
    }

    [Fact]
    public async Task LoadLog_WithMultipleRepositories_ShowsRepoCountAndSelectsRoot()
    {
        // Arrange（含嵌套子仓库时状态栏标注仓库总数；加载后默认选中根仓库）
        var vm = CreateViewModel();
        vm.RepositoryPath = @"D:\repo";
        _gitLog.Setup(g => g.GetLogAsync(@"D:\repo", It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GitLogResult.Success([
                new GitRepositoryLog("根仓库", [new GitLogEntry("a1", "A", "a@x.com", DateTimeOffset.Now, "root commit")], IsRoot: true),
                new GitRepositoryLog("sub/module", [new GitLogEntry("b2", "B", "b@x.com", DateTimeOffset.Now, "sub commit")])
            ]));

        // Act
        await vm.LoadLogCommand.ExecuteAsync(null);

        // Assert（加载摘要为总数；默认选中根仓库，LogCount 跟随根仓库条数）
        vm.StatusMessage.Should().Be("✓ 共 2 条提交（2 个仓库）");
        vm.SelectedRepository.Should().NotBeNull();
        vm.SelectedRepository!.IsRoot.Should().BeTrue();
        vm.LogCount.Should().Be(1);
        vm.HasErrors.Should().BeFalse();
    }

    [Fact]
    public async Task SwitchRepository_UpdatesLogCountAndStatus()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.RepositoryPath = @"D:\repo";
        _gitLog.Setup(g => g.GetLogAsync(@"D:\repo", It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GitLogResult.Success([
                new GitRepositoryLog("根仓库", [new GitLogEntry("a1", "A", "a@x.com", DateTimeOffset.Now, "root commit")], IsRoot: true),
                new GitRepositoryLog("sub/module", [new GitLogEntry("b2", "B", "b@x.com", DateTimeOffset.Now, "sub commit")])
            ]));
        await vm.LoadLogCommand.ExecuteAsync(null);

        // Act（切换到子仓库 Tab）
        vm.SelectedRepository = vm.Repositories[1];

        // Assert（LogCount 跟随选中仓库；状态显示切换消息；复制可用）
        vm.LogCount.Should().Be(1);
        vm.StatusMessage.Should().Be("📂 sub/module：1 条提交");
        vm.CopyLogCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task CopyLog_CopiesSelectedRepositoryEntriesOnly()
    {
        // Arrange（复制跟随当前选中仓库——切到子仓库后只复制子仓库条目，不带仓库前缀）
        var vm = CreateViewModel();
        vm.RepositoryPath = @"D:\repo";
        _gitLog.Setup(g => g.GetLogAsync(@"D:\repo", It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GitLogResult.Success([
                new GitRepositoryLog("根仓库", [new GitLogEntry("abc1234", "Test User", "test@example.com",
                    new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.FromHours(8)), "root commit")], IsRoot: true),
                new GitRepositoryLog("sub/module", [new GitLogEntry("def5678", "Test User", "test@example.com",
                    new DateTimeOffset(2026, 3, 2, 9, 30, 0, TimeSpan.FromHours(8)), "sub commit")])
            ]));
        await vm.LoadLogCommand.ExecuteAsync(null);
        vm.SelectedRepository = vm.Repositories[1];

        string? captured = null;
        _clipboard.Setup(c => c.SetTextAsync(It.IsAny<string>()))
            .Callback<string>(t => captured = t)
            .Returns(Task.CompletedTask);

        // Act
        await vm.CopyLogCommand.ExecuteAsync(null);

        // Assert（仅选中仓库条目，根仓库条目不出现）
        captured.Should().Be("def5678 | Test User | 2026-03-02 09:30\nsub commit");
    }

    [Fact]
    public async Task Ctor_WithSavedFolderAndCurrentUser_DefaultsAuthorFilterToCurrentUser()
    {
        // Arrange（当前 git 用户 = A → 加载后默认过滤为 A，仅显示 A 的提交）
        const string savedPath = @"D:\Code\Self\DS.Tools";
        _settings.Setup(s => s.Load()).Returns(new GitSettings { LastFolderPath = savedPath });
        _gitLog.Setup(g => g.IsGitRepositoryAsync(savedPath, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _gitLog.Setup(g => g.GetCurrentBranchAsync(savedPath, It.IsAny<CancellationToken>())).ReturnsAsync("main");
        _gitLog.Setup(g => g.GetCurrentUserNameAsync(savedPath, It.IsAny<CancellationToken>())).ReturnsAsync("A");
        _gitLog.Setup(g => g.GetLogAsync(savedPath, It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GitLogResult.Success([
                new GitRepositoryLog("根仓库", [
                    new GitLogEntry("a1", "A", "a@x.com", DateTimeOffset.Now, "by A"),
                    new GitLogEntry("b2", "B", "b@x.com", DateTimeOffset.Now, "by B")
                ], IsRoot: true)
            ]));

        // Act
        var vm = CreateViewModel();
        await WaitUntilAsync(() => !vm.IsLoading && vm.HasLog);

        // Assert（默认选中当前用户；条目/计数/摘要均为过滤后）
        vm.SelectedAuthorOption!.Name.Should().Be("A");
        vm.LogCount.Should().Be(1);
        vm.SelectedRepository!.Entries.Should().ContainSingle(e => e.Message == "by A");
        vm.StatusMessage.Should().Be("✓ 共 1 条提交");
        // 选项列表：全部在首，A/B 均在列
        vm.AuthorOptions[0].Name.Should().BeNull();
        vm.AuthorOptions.Select(o => o.Name).Should().Contain(["A", "B"]);
    }

    [Fact]
    public async Task AuthorFilter_ManualSelection_PersistsAcrossReload()
    {
        // Arrange（手动切到 B 后重新获取日志——不重置为默认"全部/当前用户"）
        var vm = CreateViewModel();
        vm.RepositoryPath = @"D:\repo";
        SetupLogWithAuthors(["A", "B"]);
        await vm.LoadLogCommand.ExecuteAsync(null);

        vm.SelectedAuthorOption = vm.AuthorOptions.First(o => o.Name == "B");

        // Act（换日期重新拉取，返回数据不变）
        await vm.LoadLogCommand.ExecuteAsync(null);

        // Assert
        vm.SelectedAuthorOption!.Name.Should().Be("B");
        vm.LogCount.Should().Be(1);
        vm.StatusMessage.Should().Be("✓ 共 1 条提交");
    }

    [Fact]
    public async Task AuthorFilter_SwitchToAll_RestoresAllEntries()
    {
        // Arrange（默认过滤为 A → 手动切回"全部"恢复全部条目）
        const string savedPath = @"D:\Code\Self\DS.Tools";
        _settings.Setup(s => s.Load()).Returns(new GitSettings { LastFolderPath = savedPath });
        _gitLog.Setup(g => g.IsGitRepositoryAsync(savedPath, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _gitLog.Setup(g => g.GetCurrentBranchAsync(savedPath, It.IsAny<CancellationToken>())).ReturnsAsync("main");
        _gitLog.Setup(g => g.GetCurrentUserNameAsync(savedPath, It.IsAny<CancellationToken>())).ReturnsAsync("A");
        SetupLogWithAuthors(["A", "B"], path: savedPath);
        var vm = CreateViewModel();
        await WaitUntilAsync(() => !vm.IsLoading && vm.HasLog);
        vm.LogCount.Should().Be(1);

        // Act
        vm.SelectedAuthorOption = vm.AuthorOptions[0]; // 全部提交人

        // Assert
        vm.SelectedAuthorOption!.Name.Should().BeNull();
        vm.LogCount.Should().Be(2);
        vm.StatusMessage.Should().Be("✓ 共 2 条提交");
    }

    [Fact]
    public async Task AuthorFilter_HidesRepositoriesWithoutMatches()
    {
        // Arrange（两个仓库各含不同提交人；过滤 A 后子仓库 Tab 隐藏，切回全部恢复）
        var vm = CreateViewModel();
        vm.RepositoryPath = @"D:\repo";
        _gitLog.Setup(g => g.GetLogAsync(@"D:\repo", It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GitLogResult.Success([
                new GitRepositoryLog("根仓库", [
                    new GitLogEntry("a1", "A", "a@x.com", DateTimeOffset.Now, "root by A"),
                    new GitLogEntry("b2", "B", "b@x.com", DateTimeOffset.Now, "root by B")
                ], IsRoot: true),
                new GitRepositoryLog("sub/module", [new GitLogEntry("c3", "B", "b@x.com", DateTimeOffset.Now, "sub by B")])
            ]));
        await vm.LoadLogCommand.ExecuteAsync(null);
        vm.Repositories.Should().HaveCount(2);

        // Act（过滤 A：子仓库无 A 的提交 → Tab 隐藏）
        vm.SelectedAuthorOption = vm.AuthorOptions.First(o => o.Name == "A");

        // Assert
        vm.Repositories.Should().ContainSingle();
        vm.Repositories[0].DisplayName.Should().Be("根仓库");
        vm.Repositories[0].EntryCount.Should().Be(1);

        // 切回全部：两个 Tab 恢复
        vm.SelectedAuthorOption = vm.AuthorOptions[0];
        vm.Repositories.Should().HaveCount(2);
    }

    [Fact]
    public async Task AuthorFilter_CurrentUserWithoutCommits_ShowsNoCommitsState()
    {
        // Arrange（当前 git 用户在时间范围内无提交：仍出现在选项并被默认选中，空状态提示）
        const string savedPath = @"D:\Code\Self\DS.Tools";
        _settings.Setup(s => s.Load()).Returns(new GitSettings { LastFolderPath = savedPath });
        _gitLog.Setup(g => g.IsGitRepositoryAsync(savedPath, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _gitLog.Setup(g => g.GetCurrentBranchAsync(savedPath, It.IsAny<CancellationToken>())).ReturnsAsync("main");
        _gitLog.Setup(g => g.GetCurrentUserNameAsync(savedPath, It.IsAny<CancellationToken>())).ReturnsAsync("A");
        _gitLog.Setup(g => g.GetLogAsync(savedPath, It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GitLogResult.Success([
                new GitRepositoryLog("根仓库", [new GitLogEntry("b2", "B", "b@x.com", DateTimeOffset.Now, "by B")], IsRoot: true)
            ]));

        // Act
        var vm = CreateViewModel();
        await WaitUntilAsync(() => !vm.IsLoading && vm.HasLog);

        // Assert（A 附加在选项列表并默认选中；无条目 → 空仓库 Tab 全部隐藏 → 无提交状态）
        vm.SelectedAuthorOption!.Name.Should().Be("A");
        vm.AuthorOptions.Select(o => o.Name).Should().Contain("A");
        vm.Repositories.Should().BeEmpty();
        vm.LogCount.Should().Be(0);
        vm.IsNoCommitsState.Should().BeTrue();
    }

    /// <summary>
    /// 配置根仓库日志桩：每位作者一条提交
    /// </summary>
    private void SetupLogWithAuthors(string[] authors, string path = @"D:\repo")
        => _gitLog.Setup(g => g.GetLogAsync(path, It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GitLogResult.Success([
                new GitRepositoryLog("根仓库",
                    [.. authors.Select((a, i) => new GitLogEntry($"h{i}", a, $"{a}@x.com", DateTimeOffset.Now, $"by {a}"))],
                    IsRoot: true)
            ]));

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
        vm.Repositories.Should().BeEmpty();
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
    public async Task CopyLog_WithEntries_CopiesFullMessagesToClipboard()
    {
        // Arrange（第二条含多行正文——%B 完整消息应整体复制，而非仅首行主题）
        var vm = CreateViewModel();
        vm.RepositoryPath = @"D:\repo";
        _gitLog.Setup(g => g.GetLogAsync(@"D:\repo", It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GitLogResult.Success([
                new GitRepositoryLog("根仓库", [
                    new GitLogEntry("abc1234", "Test User", "test@example.com",
                        new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.FromHours(8)), "fix: bug"),
                    new GitLogEntry("def5678", "小毛 邵", "xiaomao@example.com",
                        new DateTimeOffset(2026, 3, 2, 9, 30, 0, TimeSpan.FromHours(8)), "feat: new feature\n\nline one\n\nline three")
                ], IsRoot: true)
            ]));
        await vm.LoadLogCommand.ExecuteAsync(null);

        string? captured = null;
        _clipboard.Setup(c => c.SetTextAsync(It.IsAny<string>()))
            .Callback<string>(t => captured = t)
            .Returns(Task.CompletedTask);

        // Act
        await vm.CopyLogCommand.ExecuteAsync(null);

        // Assert（每条 = 元数据行 + 完整消息，条目间空行分隔）
        captured.Should().Contain("abc1234 | Test User | 2026-03-01 10:00\nfix: bug");
        captured.Should().Contain("def5678 | 小毛 邵 | 2026-03-02 09:30\nfeat: new feature\n\nline one\n\nline three");
        captured.Should().Contain("\n\n");
    }

    [Fact]
    public async Task CopyEntry_WithEntry_CopiesOnlyThatEntry()
    {
        // Arrange
        var vm = CreateViewModel();
        var entry = new GitLogEntry("abc1234", "Test User", "test@example.com",
            new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.FromHours(8)), "fix: bug\n\nbody line");
        string? captured = null;
        _clipboard.Setup(c => c.SetTextAsync(It.IsAny<string>()))
            .Callback<string>(t => captured = t)
            .Returns(Task.CompletedTask);

        // Act
        await vm.CopyEntryCommand.ExecuteAsync(entry);

        // Assert（仅该条完整内容，无条目间分隔符）
        captured.Should().Be("abc1234 | Test User | 2026-03-01 10:00\nfix: bug\n\nbody line");
        vm.StatusMessage.Should().Contain("已复制该条");
    }

    [Fact]
    public async Task CopyEntry_WithNullEntry_DoesNotTouchClipboard()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        await vm.CopyEntryCommand.ExecuteAsync(null);

        // Assert
        _clipboard.Verify(c => c.SetTextAsync(It.IsAny<string>()), Times.Never);
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
            .ReturnsAsync(GitLogResult.Success([new GitRepositoryLog("根仓库", [new GitLogEntry("a1", "A", "a@x.com", DateTimeOffset.Now, "s1")], IsRoot: true)]));
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
