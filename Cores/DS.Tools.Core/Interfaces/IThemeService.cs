using Avalonia.Styling;

namespace DS.Tools.Core.Interfaces;

/// <summary>
/// 主题服务接口 - 管理 Light/Dark 主题切换
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// 当前主题
    /// </summary>
    ThemeVariant CurrentTheme { get; }

    /// <summary>
    /// 切换主题
    /// </summary>
    /// <param name="theme">目标主题</param>
    void SetTheme(ThemeVariant theme);
}
