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
    public JsonFormatterResult Format(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return JsonFormatterResult.CreateFailure(
                "JSON 输入不能为空",
                JsonFormatterOperationType.Format);
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var (output, depth) = WriteJson(doc.RootElement, indented: true);

            return JsonFormatterResult.CreateSuccess(
                output,
                json.Length,
                JsonFormatterOperationType.Format,
                depth);
        }
        catch (JsonException ex)
        {
            return JsonFormatterResult.CreateFailure(
                $"JSON 格式错误: {ex.Message}",
                JsonFormatterOperationType.Format);
        }
        catch (Exception ex)
        {
            return JsonFormatterResult.CreateFailure(
                $"格式化失败: {ex.Message}",
                JsonFormatterOperationType.Format);
        }
    }

    /// <summary>
    /// 压缩 JSON 字符串（最小化）
    /// </summary>
    public JsonFormatterResult Compress(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return JsonFormatterResult.CreateFailure(
                "JSON 输入不能为空",
                JsonFormatterOperationType.Compress);
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var (output, depth) = WriteJson(doc.RootElement, indented: false);

            return JsonFormatterResult.CreateSuccess(
                output,
                json.Length,
                JsonFormatterOperationType.Compress,
                depth);
        }
        catch (JsonException ex)
        {
            return JsonFormatterResult.CreateFailure(
                $"JSON 格式错误: {ex.Message}",
                JsonFormatterOperationType.Compress);
        }
        catch (Exception ex)
        {
            return JsonFormatterResult.CreateFailure(
                $"压缩失败: {ex.Message}",
                JsonFormatterOperationType.Compress);
        }
    }

    /// <summary>
    /// 验证 JSON 字符串
    /// </summary>
    public JsonFormatterResult Validate(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return JsonFormatterResult.CreateFailure(
                "JSON 输入不能为空",
                JsonFormatterOperationType.Validate);
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var depth = CalculateElementDepth(doc.RootElement, 0);

            // 验证成功，返回格式化的 JSON 作为结果
            return JsonFormatterResult.CreateSuccess(
                "✓ JSON 格式有效",
                json.Length,
                JsonFormatterOperationType.Validate,
                depth);
        }
        catch (JsonException ex)
        {
            return JsonFormatterResult.CreateFailure(
                $"JSON 格式错误: {ex.Message}",
                JsonFormatterOperationType.Validate);
        }
        catch (Exception ex)
        {
            return JsonFormatterResult.CreateFailure(
                $"验证失败: {ex.Message}",
                JsonFormatterOperationType.Validate);
        }
    }

    /// <summary>
    /// 计算 JSON 嵌套深度
    /// </summary>
    public int CalculateJsonDepth(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return 0;

        try
        {
            using var doc = JsonDocument.Parse(json);
            return CalculateElementDepth(doc.RootElement, 0);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// 递归计算 JSON 元素深度
    /// </summary>
    private static int CalculateElementDepth(JsonElement element, int currentDepth)
    {
        int maxChildDepth = currentDepth;

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var childDepth = CalculateElementDepth(property.Value, currentDepth + 1);
                maxChildDepth = Math.Max(maxChildDepth, childDepth);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var childDepth = CalculateElementDepth(item, currentDepth + 1);
                maxChildDepth = Math.Max(maxChildDepth, childDepth);
            }
        }

        return maxChildDepth;
    }

    /// <summary>
    /// 单遍写出 JSON：递归写入 Utf8JsonWriter 并计算嵌套深度
    /// </summary>
    private static (string Text, int Depth) WriteJson(JsonElement element, bool indented)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = indented });
        var depth = WriteElement(element, writer, 0);
        writer.Flush();

        return (Encoding.UTF8.GetString(stream.ToArray()), depth);
    }

    /// <summary>
    /// 递归写入 JSON 元素，返回子树最大深度
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
