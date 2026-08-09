using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
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
public sealed class ClipboardService(ILogger<ClipboardService> logger) : IClipboardService
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
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "写入剪贴板失败");
        }
    }

    /// <summary>
    /// 从剪贴板获取文本
    /// </summary>
    public async Task<string?> GetTextAsync()
    {
        try
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (GetClipboard() is not { } clipboard)
                {
                    logger.LogWarning("剪贴板不可用（主窗口尚未就绪）");
                    return null;
                }

                // 调用方负责释放 IAsyncDataTransfer
                using var dataTransfer = await clipboard.TryGetDataAsync();
                if (dataTransfer is null)
                {
                    return null;
                }

                foreach (var item in dataTransfer.GetItems(DataFormat.Text))
                {
                    var text = await item.TryGetTextAsync();
                    if (text is not null)
                    {
                        return text;
                    }
                }

                return null;
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "读取剪贴板失败");
            return null;
        }
    }

    /// <summary>
    /// 获取当前主窗口的剪贴板（Window 继承 TopLevel）
    /// </summary>
    private static IClipboard? GetClipboard()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow }
            ? mainWindow.Clipboard
            : null;
    }
}
