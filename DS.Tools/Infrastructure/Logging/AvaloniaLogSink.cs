using System.Text.RegularExpressions;
using Avalonia.Logging;
using Microsoft.Extensions.Logging;

namespace DS.Tools.Infrastructure.Logging;

/// <summary>
/// Avalonia 内部日志桥接器 - 将 Avalonia 日志系统（含绑定错误、布局警告等）接入 ILogger。
/// 绑定/模板错误默认只输出到 Debug/Trace，接入后可在 Serilog 文件日志中排查。
/// </summary>
public sealed class AvaloniaLogSink(ILogger logger) : ILogSink
{
    /// <summary>Avalonia 命名占位符（{Name}）匹配，静态编译缓存（绑定错误日志高频时避免每次现编正则）</summary>
    private static readonly Regex NamedPlaceholderRegex = new(
        @"\{[A-Za-z][A-Za-z0-9]*\}",
        RegexOptions.Compiled);

    public bool IsEnabled(LogEventLevel level, string area)
        => logger.IsEnabled(level switch
        {
            LogEventLevel.Verbose => LogLevel.Trace,
            LogEventLevel.Debug => LogLevel.Debug,
            LogEventLevel.Information => LogLevel.Information,
            LogEventLevel.Warning => LogLevel.Warning,
            LogEventLevel.Error => LogLevel.Error,
            LogEventLevel.Fatal => LogLevel.Critical,
            _ => LogLevel.Information
        });

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
        => Log(level, area, source, messageTemplate, []);

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
    {
        var logLevel = level switch
        {
            LogEventLevel.Verbose => LogLevel.Trace,
            LogEventLevel.Debug => LogLevel.Debug,
            LogEventLevel.Information => LogLevel.Information,
            LogEventLevel.Warning => LogLevel.Warning,
            LogEventLevel.Error => LogLevel.Error,
            LogEventLevel.Fatal => LogLevel.Critical,
            _ => LogLevel.Information
        };

        // Avalonia 日志模板使用命名占位符（{Name}），与 propertyValues 按位置对应——按出现顺序替换
        var index = 0;
        var formatted = NamedPlaceholderRegex.Replace(
            messageTemplate,
            _ => index < propertyValues.Length ? propertyValues[index++]?.ToString() ?? "null" : "null");

        logger.Log(logLevel, "Avalonia[{Area}] {Message}", area, formatted);
    }
}
