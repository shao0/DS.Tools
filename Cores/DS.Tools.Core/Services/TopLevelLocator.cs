using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace DS.Tools.Core.Services;

/// <summary>
/// 顶层窗口定位助手 - 从 ApplicationLifetime 获取主窗口（Window 继承 TopLevel，提供 Clipboard/StorageProvider）。
/// 供需要 UI 线程资源访问的服务（剪贴板/文件夹选择）复用，AOT 兼容。
/// </summary>
internal static class TopLevelLocator
{
    /// <summary>
    /// 获取当前主窗口（应用尚未就绪时返回 null）
    /// </summary>
    public static Window? GetMainWindow()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow }
            ? mainWindow
            : null;
    }
}
