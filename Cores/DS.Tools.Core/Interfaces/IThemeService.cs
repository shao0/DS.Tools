using Avalonia.Styling;

namespace DS.Tools.Core.Interfaces;

/// <summary>
/// 主题服务接口 - 管理 Light/Dark/System 三态主题切换
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

    /// <summary>
    /// 主题变更事件（标准 .NET 事件）
    /// </summary>
    event Action<ThemeVariant>? ThemeChanged;

    /// <summary>
    /// 是否跟随系统主题
    /// </summary>
    bool FollowSystemTheme { get; set; }

    /// <summary>
    /// 获取当前实际应用的主题（考虑系统主题跟随）
    /// </summary>
    ThemeVariant GetActualTheme();
}