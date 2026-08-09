using System.Text.Json;
using Microsoft.Extensions.Logging;
using DS.Tools.Module.Git.Models;

namespace DS.Tools.Module.Git.Services;

/// <summary>
/// Git 模块本地设置服务实现 - 默认持久化到 <c>%LocalAppData%\DS.Tools\git-settings.json</c>。
/// 双构造函数：DI 走默认路径；测试注入临时路径。
/// </summary>
public sealed class GitSettingsService : IGitSettingsService
{
    private readonly ILogger<GitSettingsService> _logger;
    private readonly string _filePath;

    /// <summary>
    /// 构造函数（DI 使用）—— 默认路径 %LocalAppData%\DS.Tools\git-settings.json
    /// </summary>
    public GitSettingsService(ILogger<GitSettingsService> logger)
        : this(logger, GetDefaultFilePath())
    {
    }

    /// <summary>
    /// 构造函数（测试使用）—— 注入自定义设置文件路径
    /// </summary>
    public GitSettingsService(ILogger<GitSettingsService> logger, string filePath)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? GetDefaultFilePath()
            : filePath;
    }

    /// <inheritdoc />
    public GitSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new GitSettings();

            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
                return new GitSettings();

            return JsonSerializer.Deserialize(json, GitJsonContext.Default.GitSettings) ?? new GitSettings();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取 Git 设置失败（{File}），使用默认值", _filePath);
            return new GitSettings();
        }
    }

    /// <inheritdoc />
    public void Save(GitSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(settings, GitJsonContext.Default.GitSettings);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "保存 Git 设置失败（{File}）", _filePath);
        }
    }

    /// <summary>
    /// 默认设置文件路径：%LocalAppData%\DS.Tools\git-settings.json
    /// </summary>
    private static string GetDefaultFilePath()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DS.Tools",
            "git-settings.json");
}
