namespace DS.Tools.Module.Git.Models;

/// <summary>
/// 提交人过滤选项（下拉框项；<see cref="Name"/> 为 null 表示不过滤、显示全部提交人）
/// </summary>
/// <param name="Name">提交人名（%an，精确匹配；null = 全部）</param>
/// <param name="DisplayName">下拉显示名</param>
public sealed record AuthorOption(string? Name, string DisplayName)
{
    /// <summary>全部提交人（不过滤）哨兵项</summary>
    public static AuthorOption All { get; } = new(null, "全部提交人");
}
