using Xunit;
using FluentAssertions;
using Avalonia.Logging;
using Microsoft.Extensions.Logging;
using DS.Tools.Infrastructure.Logging;

namespace DS.Tools.Tests.UnitTests;

/// <summary>
/// AvaloniaLogSink 单元测试：级别映射、命名占位符替换、参数按位置填入
/// </summary>
public sealed class AvaloniaLogSinkTests
{
    /// <summary>测试用 ILogger：收集 Log 调用（ILogger 扩展方法多，手写记录器最可控）</summary>
    private sealed class ListLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }

    [Fact]
    public void Log_WithNamedPlaceholders_SubstitutesValuesInOrder()
    {
        // Arrange
        var logger = new ListLogger();
        var sink = new AvaloniaLogSink(logger);

        // Act
        sink.Log(LogEventLevel.Error, "Binding", null, "Failed to bind {Name} of {Type}", "Foo", "Bar");

        // Assert
        var entry = logger.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Error);
        entry.Message.Should().Be("Avalonia[Binding] Failed to bind Foo of Bar");
    }

    [Fact]
    public void Log_FewerValuesThanPlaceholders_UsesNullPlaceholder()
    {
        // Arrange
        var logger = new ListLogger();
        var sink = new AvaloniaLogSink(logger);

        // Act
        sink.Log(LogEventLevel.Warning, "Layout", null, "Missing {A} and {B}", "only");

        // Assert
        logger.Entries.Single().Message.Should().Be("Avalonia[Layout] Missing only and null");
    }

    [Fact]
    public void Log_MoreValuesThanPlaceholders_IgnoresExtras()
    {
        // Arrange
        var logger = new ListLogger();
        var sink = new AvaloniaLogSink(logger);

        // Act
        sink.Log(LogEventLevel.Information, "Render", null, "Only {A}", "one", "two");

        // Assert
        logger.Entries.Single().Message.Should().Be("Avalonia[Render] Only one");
    }

    [Fact]
    public void Log_NullPropertyValue_FormatsAsNull()
    {
        // Arrange
        var logger = new ListLogger();
        var sink = new AvaloniaLogSink(logger);

        // Act
        sink.Log(LogEventLevel.Information, "X", null, "Value {V}", [null]);

        // Assert
        logger.Entries.Single().Message.Should().Be("Avalonia[X] Value null");
    }

    [Theory]
    [InlineData(LogEventLevel.Verbose, LogLevel.Trace)]
    [InlineData(LogEventLevel.Debug, LogLevel.Debug)]
    [InlineData(LogEventLevel.Information, LogLevel.Information)]
    [InlineData(LogEventLevel.Warning, LogLevel.Warning)]
    [InlineData(LogEventLevel.Error, LogLevel.Error)]
    [InlineData(LogEventLevel.Fatal, LogLevel.Critical)]
    public void Log_LevelMapping_Correct(LogEventLevel avaloniaLevel, LogLevel expected)
    {
        // Arrange
        var logger = new ListLogger();
        var sink = new AvaloniaLogSink(logger);

        // Act
        sink.Log(avaloniaLevel, "Area", null, "msg");

        // Assert
        logger.Entries.Single().Level.Should().Be(expected);
    }

    [Fact]
    public void Log_NoPlaceholders_MessagePassedThrough()
    {
        // Arrange
        var logger = new ListLogger();
        var sink = new AvaloniaLogSink(logger);

        // Act
        sink.Log(LogEventLevel.Debug, "Binding", null, "plain message");

        // Assert
        logger.Entries.Single().Message.Should().Be("Avalonia[Binding] plain message");
    }

    [Fact]
    public void Log_NoArgsOverload_Works()
    {
        // Arrange
        var logger = new ListLogger();
        var sink = new AvaloniaLogSink(logger);

        // Act
        sink.Log(LogEventLevel.Warning, "Area", null, "no args");

        // Assert
        logger.Entries.Single().Message.Should().Be("Avalonia[Area] no args");
    }

    [Fact]
    public void IsEnabled_DelegatesToLogger()
    {
        // Arrange
        var logger = new ListLogger();
        var sink = new AvaloniaLogSink(logger);

        // Act & Assert
        sink.IsEnabled(LogEventLevel.Error, "Area").Should().BeTrue();
        sink.IsEnabled(LogEventLevel.Verbose, "Area").Should().BeTrue();
    }
}
