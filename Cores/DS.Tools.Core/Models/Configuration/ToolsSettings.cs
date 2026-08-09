namespace DS.Tools.Core.Models.Configuration;

/// <summary>
/// 工具设置
/// </summary>
public record class ToolsSettings
{
    /// <summary>
    /// 默认工具ID
    /// </summary>
    public required string DefaultToolId { get; init; }

    /// <summary>
    /// 启用的工具列表
    /// </summary>
    public required string[] EnabledTools { get; init; }
}