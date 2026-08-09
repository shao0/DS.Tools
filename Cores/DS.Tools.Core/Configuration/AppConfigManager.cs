using DS.Tools.Core.Models.Configuration;
using DS.Tools.Core.Models.Localization;
using DS.Tools.Core.Models.Theme;
using DS.Tools.Core.Serialization;
using DS.Tools.Core.Constants;
using System.IO;
using System.Runtime.CompilerServices;

namespace DS.Tools.Core.Configuration;

/// <summary>
/// 应用程序配置管理器 - 管理应用设置的生命周期
/// AOT 兼容
/// </summary>
public sealed class AppConfigManager : IDisposable
{
    private readonly string _configFilePath;
    private AppSettings? _cachedSettings;
    private bool _isDisposed;

    /// <summary>
    /// 配置文件路径
    /// </summary>
    public string ConfigFilePath => _configFilePath;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="configPath">配置文件路径（可选，默认使用应用目录）</param>
    public AppConfigManager(string? configPath = null)
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appDataPath, "DS.Tools");

        // 确保目录存在
        if (!Directory.Exists(appFolder))
        {
            Directory.CreateDirectory(appFolder);
        }

        _configFilePath = configPath ?? Path.Combine(appFolder, AppConstants.ConfigFileName);
    }

    /// <summary>
    /// 加载配置文件
    /// </summary>
    /// <returns>应用设置</returns>
    public AppSettings LoadSettings()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, typeof(AppConfigManager));

        if (_cachedSettings != null)
            return _cachedSettings;

        if (!File.Exists(_configFilePath))
        {
            // 返回默认设置
            _cachedSettings = GetDefaultSettings();
            return _cachedSettings;
        }

        try
        {
            var json = File.ReadAllText(_configFilePath);
            var settings = System.Text.Json.JsonSerializer.Deserialize(json, AppJsonContext.Default.AppSettings);
            _cachedSettings = settings ?? GetDefaultSettings();
            return _cachedSettings;
        }
        catch (Exception)
        {
            // 加载失败时返回默认设置
            _cachedSettings = GetDefaultSettings();
            return _cachedSettings;
        }
    }

    /// <summary>
    /// 静态构造函数 - 统一配置源生成器上下文的序列化选项
    /// </summary>
    static AppConfigManager()
    {
        var options = AppJsonContext.Default.Options;
        options.WriteIndented = true;
        options.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    }

    /// <summary>
    /// 保存配置文件
    /// </summary>
    /// <param name="settings">应用设置</param>
    public void SaveSettings(AppSettings settings)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, typeof(AppConfigManager));

        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            // 使用源生成器上下文进行 AOT 安全的序列化
            var json = System.Text.Json.JsonSerializer.Serialize(settings, AppJsonContext.Default.AppSettings);
            File.WriteAllText(_configFilePath, json);
            _cachedSettings = settings;
        }
        catch (Exception)
        {
            // 写入失败时静默处理，不影响应用主流程
        }
    }

    /// <summary>
    /// 获取默认设置
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static AppSettings GetDefaultSettings()
    {
        return new AppSettings
        {
            Logging = new LoggingSettings
            {
                DefaultLevel = LogLevel.Information,
                WriteToFile = true,
                LogFilePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DS.Tools",
                    "logs",
                    "app.log"),
                MaxFileSizeMB = 10,
                RetainedFileCount = 5
            },
            Theme = new ThemeSettings
            {
                DefaultTheme = "System",
                FollowSystemTheme = true
            },
            Localization = new LocalizationSettings
            {
                DefaultCulture = "zh-CN",
                SupportedCultures = ["zh-CN", "en-US"]
            },
            Tools = new ToolsSettings
            {
                DefaultToolId = "text-tools",
                EnabledTools = [
                    "dashboard",
                    "json-formatter",
                    "base64",
                    "color-converter",
                    "timestamp-converter",
                    "password-generator",
                    "text-hash"
                ]
            }
        };
    }

    /// <summary>
    /// 清除缓存的设置
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ClearCache()
    {
        _cachedSettings = null;
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _cachedSettings = null;
        _isDisposed = true;
    }
}