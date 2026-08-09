using System.Text.Json;
using System.Text.Json.Serialization;

namespace DS.Tools.Module.Git.Models;

/// <summary>
/// Git 模块 JSON 序列化上下文（源生成器，NativeAOT/Trim 兼容，禁运行时反射）
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GitSettings))]
internal sealed partial class GitJsonContext : JsonSerializerContext;
