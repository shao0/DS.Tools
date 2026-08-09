using Avalonia;
using Avalonia.Styling;
using DS.Tools.Core.Interfaces;

namespace DS.Tools.Core.Services;

/// <summary>
/// 主题服务实现 - 管理 Light/Dark 主题切换
/// AOT 兼容，无运行时反射
/// </summary>
internal sealed class ThemeService : IThemeService
{
    private ThemeVariant _currentTheme = ThemeVariant.Default;

    /// <summary>
    /// 当前主题
    /// </summary>
    public ThemeVariant CurrentTheme => _currentTheme;

    /// <summary>
    /// 切换主题
    /// </summary>
    public void SetTheme(ThemeVariant theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        if (_currentTheme != theme)
        {
            _currentTheme = theme;

            // 应用主题到 Avalonia 应用
            var app = Application.Current;
            if (app is not null)
            {
                app.RequestedThemeVariant = theme;
            }
        }
    }
}
