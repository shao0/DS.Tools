using System.Text.Json;
using System.Diagnostics;
using DS.Tools.Module.Text.Models;

namespace DS.Tools.Module.Text.Services;

/// <summary>
/// JSON 格式化服务实现
/// 使用 AOT 兼容的 System.Text.Json，无反射
/// </summary>
public sealed class JsonFormatterService : IJsonFormatterService
{
    private readonly JsonWriterOptions _prettyOptions = new()
    {
        Indented = true
    };

    private readonly JsonWriterOptions _compactOptions = new()
    {
        Indented = false
    };

    /// <summary>
    /// 格式化 JSON 字符串
    /// </summary>
    public async Task<JsonFormatterResult> FormatAsync(
        string json,
        int indentSize = 2,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return JsonFormatterResult.CreateFailure(
                "JSON 输入不能为空",
                JsonFormatterOperationType.Format);
        }

        try
        {
            // 验证 JSON 有效性
            using (var doc = JsonDocument.Parse(json))
            {
                // 如果有效，重新序列化为格式化的字符串
                var originalLength = json.Length;
                var formattedJson = FormatJsonDocument(doc, indentSize);
                var depth = CalculateJsonDepth(json);

                return JsonFormatterResult.CreateSuccess(
                    formattedJson,
                    originalLength,
                    JsonFormatterOperationType.Format,
                    depth);
            }
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
    /// 压缩 JSON 字符串
    /// </summary>
    public async Task<JsonFormatterResult> CompressAsync(
        string json,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return JsonFormatterResult.CreateFailure(
                "JSON 输入不能为空",
                JsonFormatterOperationType.Compress);
        }

        try
        {
            // 验证并压缩 JSON
            using (var doc = JsonDocument.Parse(json))
            {
                var originalLength = json.Length;
                var compressedJson = WriteElementCompact(doc.RootElement);
                var depth = CalculateJsonDepth(json);

                return JsonFormatterResult.CreateSuccess(
                    compressedJson,
                    originalLength,
                    JsonFormatterOperationType.Compress,
                    depth);
            }
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
    public async Task<JsonFormatterResult> ValidateAsync(
        string json,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return JsonFormatterResult.CreateFailure(
                "JSON 输入不能为空",
                JsonFormatterOperationType.Validate);
        }

        try
        {
            using (var doc = JsonDocument.Parse(json))
            {
                var depth = CalculateJsonDepth(json);

                // 验证成功，返回格式化的 JSON 作为结果
                return JsonFormatterResult.CreateSuccess(
                    "✓ JSON 格式有效",
                    json.Length,
                    JsonFormatterOperationType.Validate,
                    depth);
            }
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
    private int CalculateElementDepth(JsonElement element, int currentDepth)
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
    /// 手动格式化 JsonDocument（支持自定义缩进）
    /// </summary>
    private string FormatJsonDocument(JsonDocument doc, int indentSize)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

        WriteElement(doc.RootElement, writer, 0, indentSize);
        writer.Flush();

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// 紧凑写入 JsonElement（AOT 兼容，避免 JsonSerializer.Serialize 反射调用）
    /// </summary>
    private string WriteElementCompact(JsonElement element)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, _compactOptions);

        WriteElement(element, writer, 0, 0);
        writer.Flush();

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// 递归写入 JSON 元素
    /// </summary>
    private void WriteElement(JsonElement element, Utf8JsonWriter writer, int depth, int indentSize)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    WriteElement(property.Value, writer, depth + 1, indentSize);
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteElement(item, writer, depth + 1, indentSize);
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
    }
}