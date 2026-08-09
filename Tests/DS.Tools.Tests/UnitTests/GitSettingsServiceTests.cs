using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using DS.Tools.Module.Git.Models;
using DS.Tools.Module.Git.Services;

namespace DS.Tools.Tests.UnitTests;

/// <summary>
/// GitSettingsService 单元测试
/// 覆盖 JSON 持久化的读写往返、文件缺失/损坏等边界情况
/// </summary>
public sealed class GitSettingsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _filePath;

    public GitSettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ds-tools-tests-" + Guid.NewGuid().ToString("N"));
        _filePath = Path.Combine(_tempDir, "git-settings.json");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // 清理失败不影响测试结果
        }
    }

    private GitSettingsService CreateService() => new(NullLogger<GitSettingsService>.Instance, _filePath);

    [Fact]
    public void Save_ThenLoad_RoundTripsLastFolderPath()
    {
        // Arrange
        var service = CreateService();
        const string expectedPath = @"D:\Code\Self\DS.Tools";

        // Act
        service.Save(new GitSettings { LastFolderPath = expectedPath });
        var loaded = service.Load();

        // Assert
        loaded.LastFolderPath.Should().Be(expectedPath);
    }

    [Fact]
    public void Save_WritesIndentedCamelCaseJson()
    {
        // Arrange
        var service = CreateService();

        // Act
        service.Save(new GitSettings { LastFolderPath = @"C:\repo" });

        // Assert（源生成上下文：缩进 + camelCase 字段名）
        var json = File.ReadAllText(_filePath);
        json.Should().Contain("\"lastFolderPath\"");
        json.Should().Contain("\n");
    }

    [Fact]
    public void Load_WhenFileMissing_ReturnsDefaults()
    {
        // Act
        var loaded = CreateService().Load();

        // Assert
        loaded.Should().NotBeNull();
        loaded.LastFolderPath.Should().BeNull();
    }

    [Fact]
    public void Load_WhenFileEmpty_ReturnsDefaults()
    {
        // Arrange
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_filePath, string.Empty);

        // Act
        var loaded = CreateService().Load();

        // Assert
        loaded.LastFolderPath.Should().BeNull();
    }

    [Fact]
    public void Load_WhenJsonCorrupt_ReturnsDefaults()
    {
        // Arrange
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_filePath, "{ not valid json !!!");

        // Act
        var loaded = CreateService().Load();

        // Assert（损坏文件不抛异常，返回默认值）
        loaded.Should().NotBeNull();
        loaded.LastFolderPath.Should().BeNull();
    }

    [Fact]
    public void Load_WithUnknownFields_IgnoresUnknownFields()
    {
        // Arrange（前向兼容：旧版本写入的未知字段应被忽略；JSON 中路径反斜杠需转义）
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_filePath, """{"lastFolderPath":"C:\\repo","unknownField":123}""");

        // Act
        var loaded = CreateService().Load();

        // Assert
        loaded.LastFolderPath.Should().Be(@"C:\repo");
    }
}
