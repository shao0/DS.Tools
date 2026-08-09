using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using DS.Tools.Core.Interfaces;

namespace DS.Tools.Core.Services;

/// <summary>
/// 剪贴板服务实现 - 经主窗口的 TopLevel.Clipboard 访问系统剪贴板（Avalonia 12 异步剪贴板 API，AOT 兼容）。
/// 所有剪贴板操作必须在 UI 线程执行，由 Dispatcher 桥接。
/// </summary>
internal sealed class ClipboardService(ILogger<ClipboardService> logger) : IClipboardService
{
    /// <summary>
    /// 设置文本到剪贴板
    /// </summary>
    public async Task SetTextAsync(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        try
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (GetClipboard() is { } clipboard)
                {
                    var dataTransfer = new DataTransfer();
                    dataTransfer.Add(DataTransferItem.CreateText(text));
                    await clipboard.SetDataAsync(dataTransfer);
                }
                else
                {
                    logger.LogWarning("剪贴板不可用（主窗口尚未就绪）");
                    throw new InvalidOperationException("剪贴板不可用（主窗口尚未就绪）");
                }
            });
        }
        catch (Exception ex)
        {
            // 记录日志后向上抛出，由 ViewModel 层统一向用户呈现错误
            logger.LogWarning(ex, "写入剪贴板失败");
            throw;
        }
    }

    /// <summary>
    /// 获取当前主窗口的剪贴板（Window 继承 TopLevel）
    /// </summary>
    private static IClipboard? GetClipboard() => TopLevelLocator.GetMainWindow()?.Clipboard;
}
