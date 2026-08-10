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
    /// 获取指定时间范围内的提交日志，按仓库分组返回（每个仓库内部时间倒序）。
    /// 自动包含选中仓库目录下嵌套的子仓库（子模块/工作树/嵌套独立仓库）——
    /// 结果第一个恒为根仓库，其余为子仓库（<see cref="GitRepositoryLog.IsRoot"/> 标记）；
    /// 子仓库失败仅跳过并记日志，根仓库失败则整体失败。
    /// </summary>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="since">起始时间（含），null 表示不限</param>
    /// <param name="until">结束时间（不含边界），null 表示不限</param>
    Task<GitLogResult> GetLogAsync(string repoPath, DateTimeOffset? since, DateTimeOffset? until, CancellationToken ct = default);
}
