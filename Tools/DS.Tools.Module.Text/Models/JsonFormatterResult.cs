using System.Text.Json;

namespace DS.Tools.Module.Text.Models;

/// <summary>
/// JSON 格式化结果模型
/// 使用 record 类型和 init 属性，支持 C# 14 特性
/// </summary>
public sealed record JsonFormatterResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 格式化后的 JSON 字符串
    /// </summary>
    public string? FormattedJson { get; init; }

    /// <summary>
    /// 错误信息（如果操作失败）
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// JSON 字符数（原始）
    /// </summary>
    public int OriginalLength { get; init; }

    /// <summary>
    /// JSON 字符数（处理后）
    /// </summary>
    public int ProcessedLength { get; init; }

    /// <summary>
    /// 压缩率（百分比）
    /// </summary>
    public double CompressionRate { get; init; }

    /// <summary>
    /// JSON 深度（嵌套层级）
    /// </summary>
    public int JsonDepth { get; init; }

    /// <summary>
    /// 操作类型
    /// </summary>
    public required JsonFormatterOperationType OperationType { get; init; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static JsonFormatterResult CreateSuccess(
        string formattedJson,
        int originalLength,
        JsonFormatterOperationType operationType,
        int jsonDepth = 0)
    {
        var processedLength = formattedJson.Length;
        var compressionRate = originalLength > 0
            ? ((double)(originalLength - processedLength) / originalLength) * 100
            : 0;

        return new JsonFormatterResult
        {
            IsSuccess = true,
            FormattedJson = formattedJson,
            OriginalLength = originalLength,
            ProcessedLength = processedLength,
            CompressionRate = compressionRate,
            JsonDepth = jsonDepth,
            OperationType = operationType
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static JsonFormatterResult CreateFailure(
        string errorMessage,
        JsonFormatterOperationType operationType)
    {
        return new JsonFormatterResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            OperationType = operationType
        };
    }
}

/// <summary>
/// JSON 格式化操作类型
/// </summary>
public enum JsonFormatterOperationType
{
    /// <summary>
    /// 格式化（美化）
    /// </summary>
    Format,

    /// <summary>
    /// 压缩（最小化）
    /// </summary>
    Compress,

    /// <summary>
    /// 验证
    /// </summary>
    Validate
}