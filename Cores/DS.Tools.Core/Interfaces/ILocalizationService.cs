namespace DS.Tools.Core.Interfaces;

/// <summary>
/// 本地化服务接口
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// 当前文化
    /// </summary>
    System.Globalization.CultureInfo CurrentCulture { get; }

    /// <summary>
    /// 设置当前文化
    /// </summary>
    /// <param name="cultureName">文化名称（如 zh-CN）</param>
    void SetCulture(string cultureName);

    /// <summary>
    /// 文化变更事件
    /// </summary>
    event Action<System.Globalization.CultureInfo>? CultureChanged;

    /// <summary>
    /// 获取本地化字符串
    /// </summary>
    /// <param name="key">字符串键</param>
    /// <returns>本地化字符串</returns>
    string GetString(string key);
}