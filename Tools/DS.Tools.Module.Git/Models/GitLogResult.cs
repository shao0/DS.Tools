namespace DS.Tools.Module.Git.Models;

/// <summary>
/// Git 日志获取结果模型
/// 使用 record 类型和 init 属性，支持 C# 14 特性
/// </summary>
public sealed record GitLogResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 错误信息（如果操作失败）
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 各仓库日志分组（成功时非空；第一个恒为根仓库，其余为嵌套子仓库）
    /// </summary>
    public IReadOnlyList<GitRepositoryLog> Repositories { get; init; } = [];

    /// <summary>
    /// 全部仓库的提交条数总和
    /// </summary>
    public int TotalEntries => Repositories.Sum(r => r.Entries.Count);

    /// <summary>
    /// 创建成功结果
    /// </summary>
    /// <param name="repositories">各仓库日志分组（根仓库第一）</param>
    public static GitLogResult Success(IReadOnlyList<GitRepositoryLog> repositories) => new()
    {
        IsSuccess = true,
        Repositories = repositories
    };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static GitLogResult Failure(string message) => new()
    {
        IsSuccess = false,
        ErrorMessage = message
    };
}
