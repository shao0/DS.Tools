using DS.Tools.Module.Git.Models;

namespace DS.Tools.Module.Git.Services;

/// <summary>
/// Git 命令行服务 - 经 git CLI 获取仓库信息与提交日志（AOT 兼容，零反射）。
/// 所有操作异步执行，可取消，30 秒超时兜底。
/// </summary>
public interface IGitLogService
{
    /// <summary>
    /// 判断指定路径是否位于 Git 仓库内（工作树或 .git 目录）
    /// </summary>
    Task<bool> IsGitRepositoryAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// 获取当前分支名；游离 HEAD（detached）时返回 HEAD 短哈希；非仓库返回 null
    /// </summary>
    Task<string?> GetCurrentBranchAsync(string repoPath, CancellationToken ct = default);

    /// <summary>
    /// 获取指定时间范围内的提交日志（按时间倒序）
    /// </summary>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="since">起始时间（含），null 表示不限</param>
    /// <param name="until">结束时间（不含边界），null 表示不限</param>
    Task<GitLogResult> GetLogAsync(string repoPath, DateTimeOffset? since, DateTimeOffset? until, CancellationToken ct = default);
}
