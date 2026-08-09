using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Threading;
using DS.Tools.Core.Interfaces;

namespace DS.Tools.Core.Services;

/// <summary>
/// 剪贴板服务实现 - Avalonia版本，AOT兼容
/// </summary>
public sealed class ClipboardService : IClipboardService
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

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (desktop.MainWindow is not null)
                {
                    // 暂时使用Console输出作为占位符
                    // TODO: 实现真正的Avalonia剪贴板API调用
                    Console.WriteLine($"剪贴板复制（占位符）: {text.Substring(0, Math.Min(50, text.Length))}...");
                }
            }
        });
    }

    /// <summary>
    /// 从剪贴板获取文本
    /// </summary>
    public async Task<string?> GetTextAsync()
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (desktop.MainWindow is not null)
                {
                    // 暂时返回占位符文本
                    // TODO: 实现真正的Avalonia剪贴板API调用
                    await Task.Delay(100);
                    return "剪贴板文本（占位符）";
                }
            }
            return null;
        });
    }
}