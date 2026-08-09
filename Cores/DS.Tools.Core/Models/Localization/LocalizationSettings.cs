namespace DS.Tools.Core.Models.Localization;

/// <summary>
/// 本地化设置
/// </summary>
public record class LocalizationSettings
{
    /// <summary>
    /// 默认文化
    /// </summary>
    public required string DefaultCulture { get; init; }

    /// <summary>
    /// 支持的文化列表
    /// </summary>
    public required string[] SupportedCultures { get; init; }
}