using DS.Tools.Module.Text.Models;

namespace DS.Tools.Module.Text.Services;

/// <summary>
/// JSON 格式化服务接口
/// 定义 JSON 格式化、压缩和验证的核心操作
/// </summary>
public interface IJsonFormatterService
{
    /// <summary>
    /// 格式化 JSON 字符串（美化）
    /// </summary>
    /// <param name="json">原始 JSON 字符串</param>
    /// <param name="indentSize">缩进空格数（默认 2）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>格式化结果</returns>
    Task<JsonFormatterResult> FormatAsync(
        string json,
        int indentSize = 2,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 压缩 JSON 字符串（最小化）
    /// </summary>
    /// <param name="json">原始 JSON 字符串</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>压缩结果</returns>
    Task<JsonFormatterResult> CompressAsync(
        string json,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 验证 JSON 字符串
    /// </summary>
    /// <param name="json">待验证的 JSON 字符串</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>验证结果</returns>
    Task<JsonFormatterResult> ValidateAsync(
        string json,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 计算 JSON 嵌套深度
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>嵌套层级数</returns>
    int CalculateJsonDepth(string json);
}