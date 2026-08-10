namespace DS.Tools.Module.Git.Models;

/// <summary>
/// Git 提交日志条目（纯 UI 绑定模型，无需 JSON 序列化）。
/// 仓库归属由上层 <see cref="GitRepositoryLog"/> 分组表达，条目自身不携带
/// </summary>
/// <param name="Hash">提交短哈希（%h）</param>
/// <param name="AuthorName">作者名（%an）</param>
/// <param name="AuthorEmail">作者邮箱（%ae）</param>
/// <param name="Date">作者提交时间（%aI，严格 ISO-8601，含时区偏移）</param>
/// <param name="Message">完整提交消息（%B，含正文与换行；首行为主题）</param>
public sealed record GitLogEntry(
    string Hash,
    string AuthorName,
    string AuthorEmail,
    DateTimeOffset Date,
    string Message);
