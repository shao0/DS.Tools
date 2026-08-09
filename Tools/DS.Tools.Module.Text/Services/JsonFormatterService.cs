using System.Text;
using System.Text.Json;
using DS.Tools.Module.Text.Models;

namespace DS.Tools.Module.Text.Services;

/// <summary>
/// JSON 格式化服务实现。
/// 每次操作仅解析一次 JsonDocument，验证、输出与深度计算在同一次遍历中完成；
/// 使用 Utf8JsonWriter 输出（AOT 兼容，无反射）。
/// </summary>
public sealed class JsonFormatterService : IJsonFormatterService
{
    /// <summary>
    /// 格式化 JSON 字符串（美化）
    /// </summary>
    public JsonFormatterResult Format(string json) => Execute(json, JsonFormatterOperationType.Format, indented: true);

    /// <summary>
    /// 压缩 JSON 字符串（最小化）
    /// </summary>
    public JsonFormatterResult Compress(string json) => Execute(json, JsonFormatterOperationType.Compress, indented: false);

    /// <summary>
    /// 验证 JSON 字符串（无输出，仅确认合法并计算嵌套深度）
    /// </summary>
    public JsonFormatterResult Validate(string json)
        => Execute(json, JsonFormatterOperationType.Validate, indented: false, successText: string.Empty);

    /// <summary>
    /// 统一执行模板：空值校验 → 单次解析 → 单遍写出（同时计算深度）→ 结果包装。
    /// 验证操作无输出：写入 Stream.Null 只取深度，零输出分配。
    /// </summary>
    private static JsonFormatterResult Execute(
        string json,
        JsonFormatterOperationType operationType,
        bool indented,
        string? successText = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return JsonFormatterResult.CreateFailure("JSON 输入不能为空", operationType);
        }

        try
        {
            using var doc = JsonDocument.Parse(json);

            using var stream = successText is null ? new MemoryStream() : Stream.Null;
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = indented });
            var depth = WriteElement(doc.RootElement, writer, 0);
            writer.Flush();

            var output = successText ?? Encoding.UTF8.GetString(((MemoryStream)stream).ToArray());
            return JsonFormatterResult.CreateSuccess(output, json.Length, operationType, depth);
        }
        catch (JsonException ex)
        {
            return JsonFormatterResult.CreateFailure($"JSON 格式错误: {ex.Message}", operationType);
        }
        catch (Exception ex)
        {
            return JsonFormatterResult.CreateFailure($"{OperationName(operationType)}失败: {ex.Message}", operationType);
        }
    }

    /// <summary>
    /// 操作类型对应的失败消息前缀
    /// </summary>
    private static string OperationName(JsonFormatterOperationType operationType) => operationType switch
    {
        JsonFormatterOperationType.Format => "格式化",
        JsonFormatterOperationType.Compress => "压缩",
        _ => "验证"
    };

    /// <summary>
    /// 递归写入 JSON 元素，返回子树最大深度（单遍完成输出与深度计算）
    /// </summary>
    private static int WriteElement(JsonElement element, Utf8JsonWriter writer, int depth)
    {
        int maxDepth = depth;

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    maxDepth = Math.Max(maxDepth, WriteElement(property.Value, writer, depth + 1));
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    maxDepth = Math.Max(maxDepth, WriteElement(item, writer, depth + 1));
                }
                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;

            case JsonValueKind.Number:
                if (element.TryGetInt64(out var longValue))
                    writer.WriteNumberValue(longValue);
                else if (element.TryGetDouble(out var doubleValue))
                    writer.WriteNumberValue(doubleValue);
                else
                    writer.WriteNumberValue(element.GetDecimal());
                break;

            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;

            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;

            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
        }

        return maxDepth;
    }
}
