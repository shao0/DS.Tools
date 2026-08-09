using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Collections.Concurrent;

namespace DS.Tools.Core.Infrastructure.Logging;

/// <summary>
/// Serilog 日志工厂适配器 - AOT 兼容
/// </summary>
public sealed class SerilogLoggerFactory : ILoggerFactory, IDisposable
{
    private readonly Serilog.ILogger _serilogLogger;
    private readonly ConcurrentDictionary<string, Microsoft.Extensions.Logging.ILogger> _loggers = new();
    private bool _isDisposed;

    /// <summary>
    /// 构造函数
    /// </summary>
    public SerilogLoggerFactory()
    {
        // 配置 Serilog（AOT 兼容配置）
        _serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Is(LogEventLevel.Information)
            .WriteTo.Console()
            .WriteTo.File("logs/dstools.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();
    }

    /// <summary>
    /// 创建指定类别的日志记录器
    /// </summary>
    public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, typeof(SerilogLoggerFactory));

        return _loggers.GetOrAdd(categoryName, name =>
            new SerilogLoggerAdapter(_serilogLogger.ForContext("SourceContext", name)));
    }

    /// <summary>
    /// 添加提供程序（Serilog 不支持）
    /// </summary>
    public void AddProvider(ILoggerProvider provider)
    {
        // Serilog 不支持动态添加提供程序
        throw new NotSupportedException("Serilog does not support adding providers dynamically.");
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        // Serilog Logger 可能实现 IDisposable，需要安全释放
        if (_serilogLogger is IDisposable disposableLogger)
        {
            disposableLogger.Dispose();
        }
        _loggers.Clear();
        _isDisposed = true;
    }

    /// <summary>
    /// Serilog 日志适配器 - 将 Serilog.ILogger 适配为 Microsoft.Extensions.Logging.ILogger
    /// </summary>
    private sealed class SerilogLoggerAdapter : Microsoft.Extensions.Logging.ILogger
    {
        private readonly Serilog.ILogger _logger;

        public SerilogLoggerAdapter(Serilog.ILogger logger)
        {
            _logger = logger;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var level = logLevel switch
            {
                LogLevel.Trace => LogEventLevel.Verbose,
                LogLevel.Debug => LogEventLevel.Debug,
                LogLevel.Information => LogEventLevel.Information,
                LogLevel.Warning => LogEventLevel.Warning,
                LogLevel.Error => LogEventLevel.Error,
                LogLevel.Critical => LogEventLevel.Fatal,
                LogLevel.None => LogEventLevel.Verbose,
                _ => LogEventLevel.Information
            };

            if (_logger.IsEnabled(level))
            {
                var message = formatter?.Invoke(state, exception) ?? state?.ToString() ?? string.Empty;
                _logger.Write(level, exception, message);
            }
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            var level = logLevel switch
            {
                LogLevel.Trace => LogEventLevel.Verbose,
                LogLevel.Debug => LogEventLevel.Debug,
                LogLevel.Information => LogEventLevel.Information,
                LogLevel.Warning => LogEventLevel.Warning,
                LogLevel.Error => LogEventLevel.Error,
                LogLevel.Critical => LogEventLevel.Fatal,
                LogLevel.None => LogEventLevel.Verbose,
                _ => LogEventLevel.Information
            };

            return _logger.IsEnabled(level);
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            // Serilog 通过上下文 enricher 处理作用域
            return Disposable.Empty;
        }
    }

    /// <summary>
    /// 空的 Disposable 实现
    /// </summary>
    private sealed class Disposable : IDisposable
    {
        public static Disposable Empty { get; } = new();
        public void Dispose() { }
    }
}
