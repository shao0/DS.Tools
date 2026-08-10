using System.ComponentModel;
using System.Diagnostics;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using DS.Tools.Module.Git.Services;

namespace DS.Tools.Tests.UnitTests;

/// <summary>
/// GitLogService 集成测试 - 基于 git CLI 在临时目录创建真实仓库验证。
/// git 不可用时测试在发现期即被跳过（RequiresGitFact）。
/// </summary>
public sealed class GitLogServiceTests
{
    private readonly GitLogService _service = new(NullLogger<GitLogService>.Instance);

    /// <summary>
    /// 需要 git CLI 的测试因子（xUnit v2 无运行时动态跳过——
    /// 在发现期检查 git 可用性，不可用时置 Skip 标记跳过）
    /// </summary>
    private sealed class RequiresGitFactAttribute : FactAttribute
    {
        public RequiresGitFactAttribute()
        {
            if (!IsGitAvailable())
                Skip = "git CLI 不可用，跳过集成测试";
        }

        private static bool IsGitAvailable()
        {
            try
            {
                var psi = new ProcessStartInfo("git", "--version")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using var process = Process.Start(psi);
                process!.WaitForExit();
                return true;
            }
            catch (Win32Exception)
            {
                return false;
            }
        }
    }

    [RequiresGitFact]
    public async Task IsGitRepository_InsideRepo_ReturnsTrue()
    {
        using var repo = GitTestRepo.Create();

        (await _service.IsGitRepositoryAsync(repo.Path)).Should().BeTrue();
    }

    [RequiresGitFact]
    public async Task IsGitRepository_InsideSubdirectoryOfRepo_ReturnsTrue()
    {
        using var repo = GitTestRepo.Create();
        Directory.CreateDirectory(Path.Combine(repo.Path, "src"));
        repo.Commit("subdir commit", new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.FromHours(8)));

        var subDir = Path.Combine(repo.Path, "src");
        (await _service.IsGitRepositoryAsync(subDir)).Should().BeTrue();
    }

    [RequiresGitFact]
    public async Task IsGitRepository_InsideGitDir_ReturnsTrue()
    {
        using var repo = GitTestRepo.Create();

        var gitDir = Path.Combine(repo.Path, ".git");
        (await _service.IsGitRepositoryAsync(gitDir)).Should().BeTrue();
    }

    [RequiresGitFact]
    public async Task IsGitRepository_OutsideRepo_ReturnsFalse()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ds-tools-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            (await _service.IsGitRepositoryAsync(tempDir)).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task IsGitRepository_EmptyPath_ReturnsFalse()
    {
        (await _service.IsGitRepositoryAsync("   ")).Should().BeFalse();
    }

    [RequiresGitFact]
    public async Task GetCurrentBranch_ReturnsBranchName()
    {
        using var repo = GitTestRepo.Create();
        repo.Commit("initial", new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.FromHours(8)));

        (await _service.GetCurrentBranchAsync(repo.Path)).Should().Be("main");
    }

    [RequiresGitFact]
    public async Task GetCurrentBranch_DetachedHead_ReturnsShortHash()
    {
        using var repo = GitTestRepo.Create();
        repo.Commit("initial", new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.FromHours(8)));
        repo.Run(["checkout", "-q", "--detach"]);

        var expected = repo.Run(["rev-parse", "--short", "HEAD"]).Stdout.Trim();
        var branch = await _service.GetCurrentBranchAsync(repo.Path);

        branch.Should().NotBeNullOrEmpty();
        branch.Should().Be(expected);
    }

    [RequiresGitFact]
    public async Task GetCurrentBranch_NotARepo_ReturnsNull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ds-tools-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            (await _service.GetCurrentBranchAsync(tempDir)).Should().BeNull();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [RequiresGitFact]
    public async Task GetLog_ReturnsAllEntriesNewestFirst()
    {
        using var repo = GitTestRepo.Create();
        var offset = TimeSpan.FromHours(8);
        repo.Commit("first commit", new DateTimeOffset(2026, 1, 1, 10, 0, 0, offset));
        repo.Commit("second commit", new DateTimeOffset(2026, 2, 1, 10, 0, 0, offset));
        repo.Commit("third commit", new DateTimeOffset(2026, 3, 1, 10, 0, 0, offset));

        var result = await _service.GetLogAsync(repo.Path, null, null);

        result.IsSuccess.Should().BeTrue();
        result.Repositories[0].Entries.Should().HaveCount(3);
        // 时间倒序：最新的在前
        result.Repositories[0].Entries[0].Message.Should().Be("third commit");
        result.Repositories[0].Entries[1].Message.Should().Be("second commit");
        result.Repositories[0].Entries[2].Message.Should().Be("first commit");
        // 字段解析
        result.Repositories[0].Entries[0].Hash.Should().NotBeNullOrEmpty();
        result.Repositories[0].Entries[0].AuthorName.Should().Be("Test User");
        result.Repositories[0].Entries[0].AuthorEmail.Should().Be("test@example.com");
        result.Repositories[0].Entries[0].Date.Should().Be(new DateTimeOffset(2026, 3, 1, 10, 0, 0, offset));
    }

    [RequiresGitFact]
    public async Task GetLog_WithPipeInMessage_ParsesMessageIntact()
    {
        using var repo = GitTestRepo.Create();
        repo.Commit("fix: A|B|C", new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.FromHours(8)));

        var result = await _service.GetLogAsync(repo.Path, null, null);

        result.IsSuccess.Should().BeTrue();
        result.Repositories[0].Entries[0].Message.Should().Be("fix: A|B|C");
    }

    [RequiresGitFact]
    public async Task GetLog_WithMultiLineMessage_ParsesFullMessage()
    {
        // 回归：%B 完整消息（含正文与换行）不丢行——%s 仅首行主题是此前的显示缺陷
        using var repo = GitTestRepo.Create();
        File.AppendAllText(Path.Combine(repo.Path, "file.txt"), "x" + Environment.NewLine);
        repo.Run(["add", "-A"]);
        repo.Run(["commit", "-q", "-m", "feat: multi-line body", "-m", "line one\n\nline three"]);

        var result = await _service.GetLogAsync(repo.Path, null, null);

        result.IsSuccess.Should().BeTrue();
        result.Repositories[0].Entries.Should().ContainSingle();
        // 完整消息：主题 + 正文段落（含中间空行），而非仅首行
        result.Repositories[0].Entries[0].Message.Should().StartWith("feat: multi-line body");
        result.Repositories[0].Entries[0].Message.Should().Contain("\n\nline one\n\nline three");
    }

    [RequiresGitFact]
    public async Task GetLog_WithChineseSubjectAndAuthor_ParsesIntact()
    {
        // 回归：中文 Windows 上 git 输出为 UTF-8，若流按 ANSI 代码页（GBK）解码会乱码
        using var repo = GitTestRepo.Create();
        repo.Commit("修复：一级菜单显示问题", new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.FromHours(8)), "小毛 邵");

        var result = await _service.GetLogAsync(repo.Path, null, null);

        result.IsSuccess.Should().BeTrue();
        result.Repositories[0].Entries[0].Message.Should().Be("修复：一级菜单显示问题");
        result.Repositories[0].Entries[0].AuthorName.Should().Be("小毛 邵");
    }

    [RequiresGitFact]
    public async Task GetCurrentBranch_WithChineseBranchName_ReturnsIntactName()
    {
        // 回归：中文分支名同样依赖 UTF-8 解码
        using var repo = GitTestRepo.Create();
        repo.Commit("initial", new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.FromHours(8)));
        repo.Run(["checkout", "-q", "-b", "中文分支"]);

        (await _service.GetCurrentBranchAsync(repo.Path)).Should().Be("中文分支");
    }

    [RequiresGitFact]
    public async Task GetLog_WithDateRange_FiltersEntries()
    {
        using var repo = GitTestRepo.Create();
        var offset = TimeSpan.FromHours(8);
        repo.Commit("jan", new DateTimeOffset(2026, 1, 1, 10, 0, 0, offset));
        repo.Commit("feb", new DateTimeOffset(2026, 2, 1, 10, 0, 0, offset));
        repo.Commit("mar", new DateTimeOffset(2026, 3, 1, 10, 0, 0, offset));

        var since = new DateTimeOffset(2026, 1, 15, 0, 0, 0, offset);
        var until = new DateTimeOffset(2026, 2, 15, 0, 0, 0, offset);

        // 仅起始时间
        var sinceOnly = await _service.GetLogAsync(repo.Path, since, null);
        sinceOnly.Repositories[0].Entries.Should().HaveCount(2);
        sinceOnly.Repositories[0].Entries.Select(e => e.Message).Should().Contain(new[] { "feb", "mar" });

        // 仅结束时间
        var untilOnly = await _service.GetLogAsync(repo.Path, null, until);
        untilOnly.Repositories[0].Entries.Should().HaveCount(2);
        untilOnly.Repositories[0].Entries.Select(e => e.Message).Should().Contain(new[] { "jan", "feb" });

        // 起止时间
        var both = await _service.GetLogAsync(repo.Path, since, until);
        both.Repositories[0].Entries.Should().HaveCount(1);
        both.Repositories[0].Entries[0].Message.Should().Be("feb");
    }

    [RequiresGitFact]
    public async Task GetLog_UntilBoundary_IsExclusiveAtBoundaryInstant()
    {
        // git --until 为排他边界：边界时刻的提交被排除，边界前一天整天包含。
        // 这是 VM 把结束日期按"含当天"处理（次日零点传参）的原因。
        using var repo = GitTestRepo.Create();
        var offset = TimeSpan.FromHours(8);
        repo.Commit("on-boundary-day", new DateTimeOffset(2026, 2, 15, 10, 0, 0, offset));
        repo.Commit("previous-day", new DateTimeOffset(2026, 2, 14, 10, 0, 0, offset));

        var until = new DateTimeOffset(2026, 2, 15, 0, 0, 0, offset);
        var result = await _service.GetLogAsync(repo.Path, null, until);

        // 边界=2-15 零点：2-15 当天的提交被排除，2-14 保留
        result.Repositories[0].Entries.Should().HaveCount(1);
        result.Repositories[0].Entries[0].Message.Should().Be("previous-day");
    }

    [RequiresGitFact]
    public async Task GetLog_WithNoMatches_ReturnsSuccessWithNoEntries()
    {
        using var repo = GitTestRepo.Create();
        repo.Commit("jan", new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.FromHours(8)));

        var result = await _service.GetLogAsync(repo.Path, new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.FromHours(8)), null);

        result.IsSuccess.Should().BeTrue();
        result.Repositories[0].Entries.Should().BeEmpty();
    }

    [RequiresGitFact]
    public async Task GetLog_InEmptyRepo_ReturnsSuccessWithNoEntries()
    {
        using var repo = GitTestRepo.Create(); // 无任何提交（未出生分支）

        var result = await _service.GetLogAsync(repo.Path, null, null);

        result.IsSuccess.Should().BeTrue("空仓库不是错误状态");
        result.Repositories[0].Entries.Should().BeEmpty();
    }

    [RequiresGitFact]
    public async Task GetLog_NotARepo_ReturnsFailure()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ds-tools-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var result = await _service.GetLogAsync(tempDir, null, null);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [RequiresGitFact]
    public async Task GetLog_WithNestedSubRepo_SeparatesRepositories()
    {
        // 选中仓库目录下嵌套独立子仓库（.git 目录）——每个仓库独立分组（对应 UI Tab），根仓库恒第一。
        // 注意顺序：根仓库首次提交须在子仓库 init 之前——unborn 嵌套仓库会让根仓库
        // `git add -A` 失败（"does not have a commit checked out"），提交静默丢失
        using var root = GitTestRepo.Create();
        var offset = TimeSpan.FromHours(8);
        root.Commit("root-jan", new DateTimeOffset(2026, 1, 1, 10, 0, 0, offset));

        var subDir = Path.Combine(root.Path, "subrepo");
        Directory.CreateDirectory(subDir);
        using var sub = GitTestRepo.CreateIn(subDir);

        sub.Commit("sub-feb", new DateTimeOffset(2026, 2, 1, 10, 0, 0, offset));
        root.Commit("root-mar", new DateTimeOffset(2026, 3, 1, 10, 0, 0, offset));

        var result = await _service.GetLogAsync(root.Path, null, null);

        result.IsSuccess.Should().BeTrue();
        result.TotalEntries.Should().Be(3);
        result.Repositories.Should().HaveCount(2);

        // 根仓库分组：仅含根仓库提交（各自时间倒序，不混入子仓库条目）
        var rootRepo = result.Repositories[0];
        rootRepo.IsRoot.Should().BeTrue();
        rootRepo.DisplayName.Should().Be("根仓库");
        rootRepo.Entries.Select(e => e.Message).Should().ContainInOrder("root-mar", "root-jan");

        // 子仓库分组：相对路径显示名 + 仅含子仓库提交
        var subRepo = result.Repositories[1];
        subRepo.IsRoot.Should().BeFalse();
        subRepo.DisplayName.Should().Be("subrepo");
        subRepo.Entries.Should().ContainSingle(e => e.Message == "sub-feb");
    }

    [RequiresGitFact]
    public async Task GetLog_WithGitFileMarkerSubRepo_DetectsSubRepository()
    {
        // 子模块/工作树形态：.git 为指向 gitdir 的文本文件（而非目录）——同样应被发现并入日志。
        // gitdir 放在根仓库树外的临时目录（真实子模块在 root/.git/modules 下——遍历不进入 .git，
        // 等价于不可见），保证工作区 .git 文件是唯一发现入口。
        // 关键：指针必须指向 gitdir 本身（直接含 HEAD/objects/refs 的目录，即 <gitDir>/.git）——
        // 指向含 .git 的工作树目录会被 git 判为 "not a git repository"
        using var root = GitTestRepo.Create();
        var offset = TimeSpan.FromHours(8);
        root.Commit("root-jan", new DateTimeOffset(2026, 1, 1, 10, 0, 0, offset));

        var subDir = Path.Combine(root.Path, "submodule");
        Directory.CreateDirectory(subDir);
        var gitDir = Path.Combine(Path.GetTempPath(), "ds-tools-tests-gitdir-" + Guid.NewGuid().ToString("N"));
        var sub = GitTestRepo.CreateIn(gitDir);
        sub.Commit("sub-feb", new DateTimeOffset(2026, 2, 1, 10, 0, 0, offset));
        File.WriteAllText(
            Path.Combine(subDir, ".git"),
            $"gitdir: {Path.Combine(gitDir, ".git").Replace('\\', '/')}");

        try
        {
            var result = await _service.GetLogAsync(root.Path, null, null);

            result.IsSuccess.Should().BeTrue();
            result.Repositories.Should().HaveCount(2);
            var subRepo = result.Repositories[1];
            subRepo.IsRoot.Should().BeFalse();
            subRepo.DisplayName.Should().Be("submodule");
            subRepo.Entries.Should().ContainSingle(e => e.Message == "sub-feb");
        }
        finally
        {
            try
            {
                Directory.Delete(gitDir, recursive: true);
            }
            catch
            {
                // 清理失败不影响测试结果
            }
        }
    }

    [RequiresGitFact]
    public async Task GetLog_WithBrokenSubRepoMarker_SkipsSubRepoAndKeepsRootLog()
    {
        // 子仓库标记损坏（gitdir 指向不存在路径）→ 该子仓库跳过，根仓库结果不受影响
        using var root = GitTestRepo.Create();
        root.Commit("root-only", new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.FromHours(8)));
        var subDir = Path.Combine(root.Path, "broken-sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, ".git"), "gitdir: /nonexistent/ds-tools-test-gitdir");

        var result = await _service.GetLogAsync(root.Path, null, null);

        result.IsSuccess.Should().BeTrue();
        result.Repositories.Should().ContainSingle();
        result.Repositories[0].IsRoot.Should().BeTrue();
        result.Repositories[0].Entries.Should().ContainSingle(e => e.Message == "root-only");
    }

    [Fact]
    public async Task GetLog_WhenGitMissing_ReturnsFailure()
    {
        // 注入不存在的 git 可执行名（无 Win32Exception 冒泡，返回友好错误）
        var service = new GitLogService(NullLogger<GitLogService>.Instance, "git-not-installed-xyz");
        var tempDir = Path.Combine(Path.GetTempPath(), "ds-tools-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var result = await service.GetLogAsync(tempDir, null, null);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("无法启动 git");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    /// 临时 git 仓库测试辅助：git init + 本地身份配置 + 按日期提交
    /// </summary>
    private sealed class GitTestRepo : IDisposable
    {
        public string Path { get; }

        private GitTestRepo(string path)
        {
            Path = path;
        }

        public static GitTestRepo Create()
            => CreateIn(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ds-tools-tests-" + Guid.NewGuid().ToString("N")));

        /// <summary>
        /// 在指定目录初始化仓库（嵌套子仓库测试用；父目录须已存在）
        /// </summary>
        public static GitTestRepo CreateIn(string path)
        {
            Directory.CreateDirectory(path);
            RunGitCore(path, ["init", "-q", "-b", "main"]);
            RunGitCore(path, ["config", "user.name", "Test User"]);
            RunGitCore(path, ["config", "user.email", "test@example.com"]);
            return new GitTestRepo(path);
        }

        /// <summary>
        /// 创建一次提交（author/committer 日期经环境变量固定；authorName 可覆盖仓库默认作者）
        /// </summary>
        public void Commit(string message, DateTimeOffset date, string? authorName = null)
        {
            var stamp = date.ToString("yyyy-MM-ddTHH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture);
            var file = System.IO.Path.Combine(Path, "file.txt");
            File.AppendAllText(file, message + Environment.NewLine);

            var env = new Dictionary<string, string>
            {
                ["GIT_AUTHOR_DATE"] = stamp,
                ["GIT_COMMITTER_DATE"] = stamp
            };
            if (authorName is not null)
            {
                env["GIT_AUTHOR_NAME"] = authorName;
            }

            Run(["add", "-A"]);
            Run(["commit", "-q", "-m", message], env);
        }

        public (int ExitCode, string Stdout, string Stderr) Run(string[] args, Dictionary<string, string>? env = null)
            => RunGitCore(Path, args, env);

        private static (int ExitCode, string Stdout, string Stderr) RunGitCore(string repo, string[] args, Dictionary<string, string>? env = null)
        {
            var psi = new ProcessStartInfo("git")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-C");
            psi.ArgumentList.Add(repo);
            foreach (var arg in args)
            {
                psi.ArgumentList.Add(arg);
            }

            if (env is not null)
            {
                foreach (var (key, value) in env)
                {
                    psi.Environment[key] = value;
                }
            }

            using var process = Process.Start(psi)!;
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, stdout, stderr);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // 清理失败不影响测试结果
            }
        }
    }
}
