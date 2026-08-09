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
    /// 提交日志条目（成功时非空）
    /// </summary>
    public IReadOnlyList<GitLogEntry> Entries { get; init; } = [];

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static GitLogResult Success(IReadOnlyList<GitLogEntry> entries) => new()
    {
        IsSuccess = true,
        Entries = entries
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
