using DS.Tools.Core.Models.Localization;
using DS.Tools.Core.Models.Theme;

namespace DS.Tools.Core.Models.Configuration;

/// <summary>
/// 应用设置模型 - 使用 record 定义不可变配置
/// </summary>
public record class AppSettings
{
    /// <summary>
    /// 日志配置
    /// </summary>
    public required LoggingSettings Logging { get; init; }

    /// <summary>
    /// 主题配置
    /// </summary>
    public required ThemeSettings Theme { get; init; }

    /// <summary>
    /// 本地化配置
    /// </summary>
    public required LocalizationSettings Localization { get; init; }

    /// <summary>
    /// 工具配置
    /// </summary>
    public required ToolsSettings Tools { get; init; }
}