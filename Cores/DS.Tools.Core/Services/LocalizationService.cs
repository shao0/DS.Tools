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
        // 简化的资源查找实现 - AOT兼容
        // 在实际应用中，可以从资源文件或数据库加载本地化字符串
        // 这里提供基本的本地化字符串映射

        var localizedStrings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // 通用字符串
            ["app_title"] = _currentCulture.Name == "zh-CN" ? "DS.Tools - 开发者工具集" : "DS.Tools - Developer Utilities",
            ["toolbox"] = _currentCulture.Name == "zh-CN" ? "工具箱" : "Toolbox",
            ["settings"] = _currentCulture.Name == "zh-CN" ? "设置" : "Settings",
            ["about"] = _currentCulture.Name == "zh-CN" ? "关于" : "About",

            // 按钮文本
            ["copy"] = _currentCulture.Name == "zh-CN" ? "复制" : "Copy",
            ["paste"] = _currentCulture.Name == "zh-CN" ? "粘贴" : "Paste",
            ["clear"] = _currentCulture.Name == "zh-CN" ? "清空" : "Clear",
            ["submit"] = _currentCulture.Name == "zh-CN" ? "提交" : "Submit",
            ["cancel"] = _currentCulture.Name == "zh-CN" ? "取消" : "Cancel",
            ["save"] = _currentCulture.Name == "zh-CN" ? "保存" : "Save",
            ["delete"] = _currentCulture.Name == "zh-CN" ? "删除" : "Delete",
            ["edit"] = _currentCulture.Name == "zh-CN" ? "编辑" : "Edit",

            // 状态消息
            ["success"] = _currentCulture.Name == "zh-CN" ? "成功" : "Success",
            ["error"] = _currentCulture.Name == "zh-CN" ? "错误" : "Error",
            ["warning"] = _currentCulture.Name == "zh-CN" ? "警告" : "Warning",
            ["loading"] = _currentCulture.Name == "zh-CN" ? "加载中..." : "Loading...",

            // 工具名称
            ["text_tools"] = _currentCulture.Name == "zh-CN" ? "文本工具" : "Text Tools",
            ["json_formatter"] = _currentCulture.Name == "zh-CN" ? "JSON格式化" : "JSON Formatter",
            ["base64_converter"] = _currentCulture.Name == "zh-CN" ? "Base64编码" : "Base64 Converter",
            ["color_converter"] = _currentCulture.Name == "zh-CN" ? "颜色转换" : "Color Converter",
            ["password_generator"] = _currentCulture.Name == "zh-CN" ? "密码生成器" : "Password Generator",
            ["text_hasher"] = _currentCulture.Name == "zh-CN" ? "文本哈希" : "Text Hasher",
            ["timestamp_converter"] = _currentCulture.Name == "zh-CN" ? "时间戳转换" : "Timestamp Converter"
        };

        // 尝试获取本地化字符串，如果找不到则返回key本身
        return localizedStrings.TryGetValue(key, out var value) ? value : key;
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