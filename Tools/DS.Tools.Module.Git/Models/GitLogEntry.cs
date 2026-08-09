namespace DS.Tools.Module.Git.Models;

/// <summary>
/// Git 提交日志条目（纯 UI 绑定模型，无需 JSON 序列化）
/// </summary>
/// <param name="Hash">提交短哈希（%h）</param>
/// <param name="AuthorName">作者名（%an）</param>
/// <param name="AuthorEmail">作者邮箱（%ae）</param>
/// <param name="Date">作者提交时间（%aI，严格 ISO-8601，含时区偏移）</param>
/// <param name="Subject">提交主题（%s）</param>
public sealed record GitLogEntry(
    string Hash,
    string AuthorName,
    string AuthorEmail,
    DateTimeOffset Date,
    string Subject);
