using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using DS.Tools.Module.Git.Models;

namespace DS.Tools.Module.Git.Services;

/// <summary>
/// Git 命令行服务实现 - 经 System.Diagnostics.Process 调用 git CLI（AOT 兼容，零反射）。
/// 命令统一以 <c>git -C &lt;path&gt; ...</c> 形式执行，参数走 ArgumentList 免引号问题。
/// 双构造函数：DI 走默认 git 可执行名；测试注入自定义可执行名/路径。
/// </summary>
public sealed class GitLogService : IGitLogService
{
    /// <summary>日志条数上限（防止大仓库卡死 UI；VM 用于展示"已达上限"提示）</summary>
    internal const int MaxEntries = 1000;

    /// <summary>子仓库发现上限（防止巨型仓库内嵌套过多仓库拖垮 UI）</summary>
    private const int MaxSubRepositories = 50;

    /// <summary>目录遍历预算（防符号链接环/巨型目录树失控）</summary>
    private const int MaxWalkedDirectories = 200_000;

    /// <summary>命令执行超时（秒）</summary>
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(30);

    private readonly ILogger<GitLogService> _logger;
    private readonly string _gitExecutable;
    /// <summary>
    /// 构造函数（测试使用）—— 可注入自定义 git 可执行文件
    /// </summary>
    public GitLogService(ILogger<GitLogService> logger, string gitExecutable = "git")
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _gitExecutable = string.IsNullOrWhiteSpace(gitExecutable)
            ? "git"
            : gitExecutable;
    }

    /// <inheritdoc />
    public async Task<bool> IsGitRepositoryAsync(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        // 单次进程同时探测工作树与 .git 目录（两个 flag 各输出一行 true/false；
        // 覆盖工作树与选中 .git 目录本身两种情况，避免两次串行启动）
        var result = await RunGitAsync(path, ["rev-parse", "--is-inside-work-tree", "--is-inside-git-dir"], ct);
        return result.ExitCode == 0
            && result.Stdout.Split('\n').Any(line => line.Trim() == "true");
    }

    /// <inheritdoc />
    public async Task<string?> GetCurrentBranchAsync(string repoPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(repoPath))
            return null;

        // 正常分支：symbolic-ref 输出分支名；非零退出 = 游离 HEAD（detached）
        var branch = await RunGitAsync(repoPath, ["symbolic-ref", "--short", "-q", "HEAD"], ct);
        if (branch.ExitCode == 0 && !string.IsNullOrWhiteSpace(branch.Stdout))
            return branch.Stdout.Trim();

        if (branch.ExitCode < 0)
            return null; // 进程未能启动（如 git 未安装）—— 不继续探测

        // 游离 HEAD：退化显示 HEAD 短哈希
        var head = await RunGitAsync(repoPath, ["rev-parse", "--short", "HEAD"], ct);
        return head.ExitCode == 0 && !string.IsNullOrWhiteSpace(head.Stdout)
            ? head.Stdout.Trim()
            : null;
    }

    /// <inheritdoc />
    public async Task<GitLogResult> GetLogAsync(string repoPath, DateTimeOffset? since, DateTimeOffset? until, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(repoPath))
            return GitLogResult.Failure("仓库路径为空");

        // 归一化路径：GetFullPath 解析相对路径/`..`，TrimEndingDirectorySeparator 保证根仓库自身
        // 识别比较（.git 发现遍历返回的目录路径不含结尾分隔符）一致
        var rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repoPath));

        // 根仓库日志——失败即整体失败（与原先语义一致）
        var rootResult = await FetchLogAsync(rootPath, since, until, ct);
        if (!rootResult.IsSuccess)
            return GitLogResult.Failure(rootResult.ErrorMessage ?? "获取日志失败");

        // 根仓库恒第一；嵌套子仓库（子模块/工作树/嵌套独立仓库）按显示名排序（稳定顺序便于切换）。
        // 子仓库失败仅跳过并记日志，不阻断根仓库结果；空仓库（时间范围内无提交）仍列出，便于用户知晓其存在
        var repositories = new List<GitRepositoryLog>
        {
            new("根仓库", rootResult.Entries, IsRoot: true)
        };

        foreach (var subRoot in FindSubRepositoryRoots(rootPath))
        {
            if (ct.IsCancellationRequested)
                break;

            var subResult = await FetchLogAsync(subRoot, since, until, ct);
            if (!subResult.IsSuccess)
            {
                _logger.LogWarning("子仓库日志获取失败，已跳过（{SubRepo}）：{Message}", subRoot, subResult.ErrorMessage);
                continue;
            }

            var repositoryName = Path.GetRelativePath(rootPath, subRoot).Replace('\\', '/');
            repositories.Add(new GitRepositoryLog(repositoryName, subResult.Entries));
        }

        return GitLogResult.Success(
            repositories.OrderBy(r => !r.IsRoot).ThenBy(r => r.DisplayName, StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// 拉取单个仓库的日志（起止日期过滤 + 条数上限 + 空仓库特判）
    /// </summary>
    private async Task<FetchResult> FetchLogAsync(string repoPath, DateTimeOffset? since, DateTimeOffset? until, CancellationToken ct)
    {
        var args = new List<string>
        {
            "log",
            $"-n {MaxEntries}"
        };
        if (since is { } sinceDate)
            args.Add($"--since={FormatGitDate(sinceDate)}");
        if (until is { } untilDate)
            args.Add($"--until={FormatGitDate(untilDate)}");

        // 记录分隔 %x1e、字段分隔 %x1f——规避提交消息含 '|' 等常规字符的解析歧义；
        // 消息取 %B（完整提交消息，含正文与换行）而非 %s（仅首行主题）
        args.Add("--pretty=format:%x1e%h%x1f%an%x1f%ae%x1f%aI%x1f%B");

        var output = await RunGitAsync(repoPath, args, ct);
        if (output.ExitCode != 0)
        {
            // 空仓库（未出生分支）特判：exit 128 + "does not have any commits yet" 是正常空状态而非错误
            if (output.Stderr.Contains("does not have any commits yet", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("仓库 {RepoPath} 尚无提交（未出生分支）", repoPath);
                return new FetchResult(true, null, []);
            }

            var message = string.IsNullOrWhiteSpace(output.Stderr)
                ? $"git log 执行失败（退出码 {output.ExitCode}）"
                : output.Stderr.Trim();
            _logger.LogWarning("git log 失败（{RepoPath}）：{Message}", repoPath, message);
            return new FetchResult(false, message, []);
        }

        return new FetchResult(true, null, ParseLogOutput(output.Stdout));
    }

    /// <summary>
    /// 发现根仓库目录下嵌套的子仓库根目录（DFS，不进入 .git 内部）。
    /// 仓库标记：.git 目录（嵌套独立仓库）或 .git 文件（子模块/工作树 gitdir 指针）；
    /// 根仓库自身的 .git 跳过，符号链接/联接点跳过（防环）。
    /// </summary>
    private static IReadOnlyList<string> FindSubRepositoryRoots(string rootPath)
    {
        var results = new List<string>();
        var stack = new Stack<string>();
        stack.Push(rootPath);
        var walked = 0;

        while (stack.Count > 0 && results.Count < MaxSubRepositories)
        {
            var dir = stack.Pop();
            if (walked++ >= MaxWalkedDirectories)
                break;

            IEnumerable<string> children;
            try
            {
                children = Directory.EnumerateFileSystemEntries(dir);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue; // 无权限/目录已删除——跳过该目录，不影响其余遍历
            }

            foreach (var child in children)
            {
                if (Path.GetFileName(child).Equals(".git", StringComparison.OrdinalIgnoreCase))
                {
                    // .git 标记所在目录即仓库根（根仓库自身除外）；
                    // 同时不把 .git 压栈——其内部（gitdir 指针文件/钩子）不是工作区仓库
                    if (!string.Equals(dir, rootPath, StringComparison.OrdinalIgnoreCase))
                        results.Add(dir);
                    continue;
                }

                if (IsRealDirectory(child))
                    stack.Push(child);
            }
        }

        return results;
    }

    /// <summary>
    /// 判断是否为真实目录（排除符号链接/联接点——防止目录环导致遍历失控）
    /// </summary>
    private static bool IsRealDirectory(string path)
    {
        try
        {
            return Directory.Exists(path)
                && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// 解析 git log 输出：\x1e 记录分隔、\x1f 字段分隔。
    /// 记录按 \x1e 整体切分（而非按行）——消息为末字段可含换行（%B 全文），跨行消息不丢行；
    /// Split 限 5 段，消息保留第 5 段之后的全部内容（含内部 \x1f）；畸形记录跳过并记日志。
    /// </summary>
    private IReadOnlyList<GitLogEntry> ParseLogOutput(string stdout)
    {
        var entries = new List<GitLogEntry>();
        var records = stdout.Split('\x1e', StringSplitOptions.RemoveEmptyEntries);

        foreach (var record in records)
        {
            // 前 4 个定长字段（hash/作者/邮箱/日期不含 \x1f 与换行）+ 消息（剩余全部，可跨行）
            var fields = record.Split('\x1f', 5);
            if (fields.Length != 5 ||
                !DateTimeOffset.TryParse(fields[3], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                var preview = record.Length > 200 ? record[..200] + "…" : record;
                _logger.LogWarning("跳过无法解析的 git log 记录：{Record}", preview);
                continue;
            }

            // git 记录之间以 \n 分隔——去掉消息尾部该分隔换行（正文内部换行保留）
            entries.Add(new GitLogEntry(
                fields[0],
                fields[1],
                fields[2],
                date,
                fields[4].TrimEnd('\n', '\r')));
        }

        return entries;
    }

    /// <summary>
    /// git 时间参数格式（ISO-8601 含时区偏移，git 原生解析）
    /// </summary>
    private static string FormatGitDate(DateTimeOffset value)
        => value.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);

    /// <summary>
    /// 执行 git 命令（防死锁：先起输出读取，再等待退出；30 秒超时 + 进程树终止）
    /// </summary>
    private async Task<GitCommandOutput> RunGitAsync(string repoPath, IReadOnlyList<string> args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _gitExecutable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            // 关键：git 输出为 UTF-8 字节，流必须按 UTF-8 解码——
            // 否则中文 Windows 默认按 ANSI 代码页（GBK）解码，中文提交主题/作者名/分支名会乱码
            StandardOutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardErrorEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        };
        psi.ArgumentList.Add("-C");
        psi.ArgumentList.Add(repoPath);
        // 强制 git 输出统一转码为 UTF-8（兼容提交编码为 GBK 的旧仓库）
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("i18n.logOutputEncoding=UTF-8");
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = psi };

        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            _logger.LogWarning(ex, "无法启动 git 可执行文件（{Git}）", _gitExecutable);
            return new GitCommandOutput(-1, string.Empty, $"无法启动 git：{ex.Message}");
        }

        // 先读取输出再等待退出，避免管道缓冲区满造成死锁
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(CommandTimeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "终止超时 git 进程失败");
            }

            return new GitCommandOutput(-1, string.Empty, "git 命令执行超时");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        return new GitCommandOutput(process.ExitCode, stdout, stderr);
    }

    /// <summary>
    /// git 命令执行结果（私有传输对象）
    /// </summary>
    private sealed record GitCommandOutput(int ExitCode, string Stdout, string Stderr);

    /// <summary>
    /// 单仓库日志拉取结果（私有传输对象；空仓库 = 成功且零条目）
    /// </summary>
    private sealed record FetchResult(bool IsSuccess, string? ErrorMessage, IReadOnlyList<GitLogEntry> Entries);
}
