using Avalonia;
using Avalonia.Styling;
using DS.Tools.Core.Interfaces;

namespace DS.Tools.Core.Services;

/// <summary>
/// 主题服务实现 - 管理 Light/Dark/System 三态主题切换
/// AOT 兼容，无运行时反射
/// </summary>
public sealed class ThemeService : IThemeService
{
    private ThemeVariant _currentTheme = ThemeVariant.Default;
    private bool _followSystemTheme = true;

    /// <summary>
    /// 当前主题
    /// </summary>
    public ThemeVariant CurrentTheme => _currentTheme;

    /// <summary>
    /// 是否跟随系统主题
    /// </summary>
    public bool FollowSystemTheme
    {
        get => _followSystemTheme;
        set
        {
            if (_followSystemTheme != value)
            {
                _followSystemTheme = value;
                if (value)
                {
                    SetTheme(ThemeVariant.Default);
                }
            }
        }
    }

    /// <summary>
    /// 主题变更事件
    /// </summary>
    public event Action<ThemeVariant>? ThemeChanged;

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
            var app = Avalonia.Application.Current;
            if (app is not null)
            {
                app.RequestedThemeVariant = theme;
            }

            ThemeChanged?.Invoke(theme);
        }
    }

    /// <summary>
    /// 获取当前实际应用的主题（考虑系统主题跟随）
    /// </summary>
    public ThemeVariant GetActualTheme()
    {
        return _followSystemTheme ? ThemeVariant.Default : _currentTheme;
    }
}