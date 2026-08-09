namespace DS.Tools.Core.Interfaces;

/// <summary>
/// 剪贴板服务接口 - AOT兼容
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// 设置文本到剪贴板
    /// </summary>
    Task SetTextAsync(string text);

    /// <summary>
    /// 从剪贴板获取文本
    /// </summary>
    Task<string?> GetTextAsync();
}