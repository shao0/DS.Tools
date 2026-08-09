using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using DS.Tools.Core.Models.Configuration;
using DS.Tools.Core.Models.Theme;
using DS.Tools.Core.Models.Localization;

namespace DS.Tools.Core.Serialization;

/// <summary>
/// JSON 序列化上下文 - 使用 Source Generator 实现 AOT 兼容序列化
/// 所有需要序列化的类型都必须通过 [JsonSerializable] 特性注册
/// </summary>
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(LoggingSettings))]
[JsonSerializable(typeof(ThemeSettings))]
[JsonSerializable(typeof(LocalizationSettings))]
[JsonSerializable(typeof(ToolsSettings))]
[JsonSerializable(typeof(LogLevel))]
[JsonSerializable(typeof(ColorInfo))]
public partial class AppJsonContext : JsonSerializerContext;