using System.Globalization;
using DS.Tools.Core.Interfaces;

namespace DS.Tools.Core.Services;

/// <summary>
/// 本地化服务实现 - 简化版，AOT 兼容
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
    private CultureInfo _currentCulture = CultureInfo.GetCultureInfo("zh-CN");

    /// <summary>
    /// 当前文化
    /// </summary>
    public CultureInfo CurrentCulture => _currentCulture;

    /// <summary>
    /// 支持的文化列表
    /// </summary>
    public IReadOnlyList<CultureInfo> SupportedCultures { get; } = new List<CultureInfo>
    {
        CultureInfo.GetCultureInfo("zh-CN"),
        CultureInfo.GetCultureInfo("en-US")
    };

    /// <summary>
    /// 文化变更事件
    /// </summary>
    public event Action<CultureInfo>? CultureChanged;

    /// <summary>
    /// 设置当前文化
    /// </summary>
    public void SetCulture(string cultureName)
    {
        var newCulture = CultureInfo.GetCultureInfo(cultureName);
        if (_currentCulture != newCulture)
        {
            _currentCulture = newCulture;
            CultureChanged?.Invoke(newCulture);
        }
    }

    /// <summary>
    /// 获取本地化字符串
    /// </summary>
    public string GetString(string key)
    {
        // TODO: 实现实际的资源文件查找
        return key;
    }

    /// <summary>
    /// 获取本地化字符串（带默认值）
    /// </summary>
    public string GetStringOrDefault(string key, string defaultValue, params object[] args)
    {
        var value = GetString(key);
        return value == key ? defaultValue : string.Format(value, args);
    }

    /// <summary>
    /// 检查文化是否支持
    /// </summary>
    public bool IsCultureSupported(CultureInfo culture)
    {
        return SupportedCultures.Any(c => c.Name == culture.Name);
    }
}