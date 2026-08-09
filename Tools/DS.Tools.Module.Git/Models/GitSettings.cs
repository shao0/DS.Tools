namespace DS.Tools.Module.Git.Models;

/// <summary>
/// Git 工具模块本地设置（JSON 持久化，源生成上下文 AOT 兼容）
/// </summary>
public sealed record GitSettings
{
    /// <summary>
    /// 最近选择的 Git 仓库文件夹路径
    /// </summary>
    public string? LastFolderPath { get; init; }
}
