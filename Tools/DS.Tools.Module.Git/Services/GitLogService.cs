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

    /// <summary>命令执行超时（秒）</summary>
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(30);

    private readonly ILogger<GitLogService> _logger;
    private readonly string _gitExecutable;

    /// <summary>
    /// 构造函数（DI 使用）—— 默认使用 PATH 中的 git
    /// </summary>
    public GitLogService(ILogger<GitLogService> logger) : this(logger, "git")
    {
    }

    /// <summary>
    /// 构造函数（测试使用）—— 可注入自定义 git 可执行文件
    /// </summary>
    public GitLogService(ILogger<GitLogService> logger, string gitExecutable)
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

        var args = new List<string>
        {
            "log",
            $"-n {MaxEntries}"
        };
        if (since is { } sinceDate)
            args.Add($"--since={FormatGitDate(sinceDate)}");
        if (until is { } untilDate)
            args.Add($"--until={FormatGitDate(untilDate)}");

        // 记录分隔 %x1e、字段分隔 %x1f——规避提交主题含 '|' 等常规字符的解析歧义
        args.Add("--pretty=format:%x1e%h%x1f%an%x1f%ae%x1f%aI%x1f%s");

        var output = await RunGitAsync(repoPath, args, ct);
        if (output.ExitCode != 0)
        {
            // 空仓库（未出生分支）特判：exit 128 + "does not have any commits yet" 是正常空状态而非错误
            if (output.Stderr.Contains("does not have any commits yet", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("仓库 {RepoPath} 尚无提交（未出生分支）", repoPath);
                return GitLogResult.Success([]);
            }

            var message = string.IsNullOrWhiteSpace(output.Stderr)
                ? $"git log 执行失败（退出码 {output.ExitCode}）"
                : output.Stderr.Trim();
            _logger.LogWarning("git log 失败（{RepoPath}）：{Message}", repoPath, message);
            return GitLogResult.Failure(message);
        }

        return GitLogResult.Success(ParseLogOutput(output.Stdout));
    }

    /// <summary>
    /// 解析 git log 输出：按 \n 分行、\x1e 记录分隔、\x1f 字段分隔；畸形行跳过并记日志
    /// </summary>
    private IReadOnlyList<GitLogEntry> ParseLogOutput(string stdout)
    {
        var entries = new List<GitLogEntry>();
        var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            var record = line.TrimStart('\x1e');
            var fields = record.Split('\x1f');

            if (fields.Length != 5 ||
                !DateTimeOffset.TryParse(fields[3], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                _logger.LogWarning("跳过无法解析的 git log 行：{Line}", line);
                continue;
            }

            entries.Add(new GitLogEntry(
                fields[0],
                fields[1],
                fields[2],
                date,
                fields[4]));
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
}
