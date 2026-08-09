using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

namespace DS.Tools.Infrastructure.Logging;

/// <summary>
/// Serilog 日志配置 - 组合根工厂。
/// 从 appsettings.json 读取：Logging:DefaultLevel（最低级别，默认 Information）、
/// Logging:WriteToFile / LogFilePath / MaxFileSizeMB / RetainedFileCount（文件输出，默认开启）。
/// </summary>
public static class SerilogConfig
{
    /// <summary>
    /// 创建 Serilog Logger（控制台 + 按配置滚动的文件）
    /// </summary>
    public static Serilog.ILogger CreateLogger(IConfiguration configuration)
    {
        var minLevel = Enum.TryParse<LogEventLevel>(
            configuration["Logging:DefaultLevel"],
            ignoreCase: true,
            out var level)
            ? level
            : LogEventLevel.Information;

        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Is(minLevel)
            .WriteTo.Console();

        // 手动解析配置键（避免引入 Configuration.Binder 包）
        var writeToFile = !bool.TryParse(configuration["Logging:WriteToFile"], out var write) || write;
        var maxFileSizeMb = int.TryParse(configuration["Logging:MaxFileSizeMB"], out var size) ? size : 10;
        var retainedFileCount = int.TryParse(configuration["Logging:RetainedFileCount"], out var count) ? count : 5;

        if (writeToFile)
        {
            var filePath = configuration["Logging:LogFilePath"] ?? "logs/app.log";

            loggerConfiguration.WriteTo.File(
                filePath,
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: maxFileSizeMb * 1024 * 1024,
                retainedFileCountLimit: retainedFileCount);
        }

        return loggerConfiguration.CreateLogger();
    }
}
