using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using DS.Tools.Infrastructure.Logging;

namespace DS.Tools.Tests.UnitTests;

/// <summary>
/// SerilogConfig 单元测试：appsettings.json 键解析与默认值回退
/// </summary>
public sealed class SerilogConfigTests
{
    private static IConfiguration CreateConfig(params (string Key, string Value)[] settings)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => (string?)s.Value))
            .Build();

    [Fact]
    public void CreateLogger_NoConfig_UsesInformationDefault()
    {
        // Arrange
        var config = CreateConfig();

        // Act
        var logger = SerilogConfig.CreateLogger(config);

        // Assert
        logger.Should().NotBeNull();
        logger.IsEnabled(LogEventLevel.Information).Should().BeTrue();
        logger.IsEnabled(LogEventLevel.Debug).Should().BeFalse("默认最低级别为 Information");
    }

    [Theory]
    [InlineData("Debug", LogEventLevel.Debug)]
    [InlineData("Verbose", LogEventLevel.Verbose)]
    [InlineData("Warning", LogEventLevel.Warning)]
    public void CreateLogger_ConfiguredLevel_Respected(string levelName, LogEventLevel level)
    {
        // Arrange
        var config = CreateConfig(("Logging:DefaultLevel", levelName));

        // Act
        var logger = SerilogConfig.CreateLogger(config);

        // Assert
        logger.IsEnabled(level).Should().BeTrue();
        logger.IsEnabled(LogEventLevel.Verbose).Should().Be(level == LogEventLevel.Verbose);
    }

    [Fact]
    public void CreateLogger_InvalidLevel_FallsBackToInformation()
    {
        // Arrange
        var config = CreateConfig(("Logging:DefaultLevel", "NotALevel"));

        // Act
        var logger = SerilogConfig.CreateLogger(config);

        // Assert
        logger.IsEnabled(LogEventLevel.Information).Should().BeTrue();
        logger.IsEnabled(LogEventLevel.Debug).Should().BeFalse();
    }

    [Fact]
    public void CreateLogger_CustomFileSettings_NoThrow()
    {
        // 自定义文件滚动配置应被接受（不抛异常）
        // Arrange
        var config = CreateConfig(
            ("Logging:WriteToFile", "true"),
            ("Logging:LogFilePath", "logs/test.log"),
            ("Logging:MaxFileSizeMB", "5"),
            ("Logging:RetainedFileCount", "3"));

        // Act
        var logger = SerilogConfig.CreateLogger(config);

        // Assert
        logger.Should().NotBeNull();
    }

    [Fact]
    public void CreateLogger_WriteToFileFalse_DisablesFileSink()
    {
        // WriteToFile=false 时创建 logger 不应抛异常（文件 sink 被跳过）
        // Arrange
        var config = CreateConfig(("Logging:WriteToFile", "false"));

        // Act
        var logger = SerilogConfig.CreateLogger(config);

        // Assert
        logger.Should().NotBeNull();
        logger.IsEnabled(LogEventLevel.Information).Should().BeTrue();
    }
}
