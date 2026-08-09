using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using DS.Tools.Core.Interfaces;

namespace DS.Tools.Core.Services;

/// <summary>
/// 文件夹选择服务实现 - 经主窗口的 TopLevel.StorageProvider 访问系统文件夹对话框（Avalonia 12，AOT 兼容）。
/// 所有对话框操作必须在 UI 线程执行，由 Dispatcher 桥接（与 ClipboardService 同模式）。
/// 对话框标题由调用方（模块）提供——Core 层不持有模块专属文案。
/// </summary>
internal sealed class FolderPickerService(ILogger<FolderPickerService> logger) : IFolderPickerService
{
    /// <summary>
    /// 打开系统文件夹选择对话框
    /// </summary>
    /// <param name="suggestedPath">建议起始位置（上次选择的文件夹），可空</param>
    /// <param name="title">对话框标题（调用方提供，null 时用系统默认）</param>
    public async Task<string?> PickFolderAsync(string? suggestedPath, string? title)
    {
        try
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (TopLevelLocator.GetMainWindow() is not { } mainWindow)
                {
                    logger.LogWarning("文件夹选择器不可用（主窗口尚未就绪）");
                    return null;
                }

                var provider = mainWindow.StorageProvider;
                if (!provider.CanPickFolder)
                {
                    logger.LogWarning("当前平台不支持文件夹选择");
                    return null;
                }

                var options = new FolderPickerOpenOptions
                {
                    Title = title,
                    AllowMultiple = false
                };

                // 建议起始位置（上次选择的文件夹），解析失败时忽略即可
                if (!string.IsNullOrWhiteSpace(suggestedPath))
                {
                    options.SuggestedStartLocation = await provider.TryGetFolderFromPathAsync(suggestedPath);
                }

                var folders = await provider.OpenFolderPickerAsync(options);
                return folders.FirstOrDefault()?.TryGetLocalPath();
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "打开文件夹选择器失败");
            return null;
        }
    }
}
