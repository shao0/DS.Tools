using DS.Tools.Module.Text.Models;

namespace DS.Tools.Module.Text.Services;

/// <summary>
/// JSON 格式化服务接口 - 定义 JSON 格式化、压缩和验证的核心操作。
/// 纯内存计算，同步 API（调用方在 UI 线程执行，数据量小无阻塞风险）。
/// </summary>
public interface IJsonFormatterService
{
    /// <summary>
    /// 格式化 JSON 字符串（美化）
    /// </summary>
    /// <param name="json">原始 JSON 字符串</param>
    /// <returns>格式化结果</returns>
    JsonFormatterResult Format(string json);

    /// <summary>
    /// 压缩 JSON 字符串（最小化）
    /// </summary>
    /// <param name="json">原始 JSON 字符串</param>
    /// <returns>压缩结果</returns>
    JsonFormatterResult Compress(string json);

    /// <summary>
    /// 验证 JSON 字符串（无输出，结果携带嵌套深度）
    /// </summary>
    /// <param name="json">待验证的 JSON 字符串</param>
    /// <returns>验证结果</returns>
    JsonFormatterResult Validate(string json);
}
