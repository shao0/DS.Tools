namespace DS.Tools.Module.Git.Models;

/// <summary>
/// 单个仓库的提交日志分组（根仓库或嵌套子仓库），对应 UI 中一个 Tab
/// </summary>
/// <param name="DisplayName">显示名：根仓库 = "根仓库"，子仓库 = 相对根目录的路径（如 sub/module）</param>
/// <param name="Entries">该仓库的提交日志（git 原生时间倒序）</param>
/// <param name="IsRoot">是否为根仓库（选中文件夹对应的仓库，恒排第一）</param>
public sealed record GitRepositoryLog(
    string DisplayName,
    IReadOnlyList<GitLogEntry> Entries,
    bool IsRoot = false)
{
    /// <summary>该仓库的提交条数</summary>
    public int EntryCount => Entries.Count;
}
