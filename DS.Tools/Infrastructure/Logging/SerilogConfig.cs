using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

namespace DS.Tools.Infrastructure.Logging;

/// <summary>
/// Serilog 日志配置 - 组合根工厂。
/// 从 appsettings.json 的 Logging:DefaultLevel 读取最低级别（默认 Information）。
/// </summary>
public static class SerilogConfig
{
    /// <summary>
    /// 创建 Serilog Logger（控制台 + 每日滚动文件）
    /// </summary>
    public static Serilog.ILogger CreateLogger(IConfiguration configuration)
    {
        var minLevel = Enum.TryParse<LogEventLevel>(
            configuration["Logging:DefaultLevel"],
            ignoreCase: true,
            out var level)
            ? level
            : LogEventLevel.Information;

        return new LoggerConfiguration()
            .MinimumLevel.Is(minLevel)
            .WriteTo.Console()
            .WriteTo.File("logs/dstools.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();
    }
}
