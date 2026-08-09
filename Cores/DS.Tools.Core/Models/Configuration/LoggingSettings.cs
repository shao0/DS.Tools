namespace DS.Tools.Core.Models.Configuration;

/// <summary>
/// 日志配置
/// </summary>
public record class LoggingSettings
{
    /// <summary>
    /// 默认日志级别
    /// </summary>
    public required LogLevel DefaultLevel { get; init; }

    /// <summary>
    /// 是否写入文件
    /// </summary>
    public required bool WriteToFile { get; init; }

    /// <summary>
    /// 日志文件路径
    /// </summary>
    public required string LogFilePath { get; init; }

    /// <summary>
    /// 最大文件大小（MB）
    /// </summary>
    public required int MaxFileSizeMB { get; init; }

    /// <summary>
    /// 保留文件数量
    /// </summary>
    public required int RetainedFileCount { get; init; }
}