using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using DS.Tools.Core.Interfaces;

namespace DS.Tools.Core.Services;

/// <summary>
/// 文件夹选择服务实现 - 经主窗口的 TopLevel.StorageProvider 访问系统文件夹对话框（Avalonia 12，AOT 兼容）。
/// 所有对话框操作必须在 UI 线程执行，由 Dispatcher 桥接（与 ClipboardService 同模式）。
/// </summary>
public sealed class FolderPickerService(ILogger<FolderPickerService> logger) : IFolderPickerService
{
    /// <summary>
    /// 打开系统文件夹选择对话框
    /// </summary>
    public async Task<string?> PickFolderAsync(string? suggestedPath)
    {
        try
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (GetMainWindow() is not { } mainWindow)
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
                    Title = "选择 Git 仓库文件夹",
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

    /// <summary>
    /// 获取当前主窗口（Window 继承 TopLevel，提供 StorageProvider）
    /// </summary>
    private static Window? GetMainWindow()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow }
            ? mainWindow
            : null;
    }
}
