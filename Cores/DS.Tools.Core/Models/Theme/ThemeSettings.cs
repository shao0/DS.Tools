namespace DS.Tools.Core.Models.Theme;

/// <summary>
/// 主题设置
/// </summary>
public record class ThemeSettings
{
    /// <summary>
    /// 默认主题（Light/Dark/System）
    /// </summary>
    public required string DefaultTheme { get; init; }

    /// <summary>
    /// 是否跟随系统主题
    /// </summary>
    public required bool FollowSystemTheme { get; init; }
}