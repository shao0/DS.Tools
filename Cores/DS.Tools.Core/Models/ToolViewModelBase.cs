using CommunityToolkit.Mvvm.ComponentModel;
using DS.Tools.Core.Interfaces;

namespace DS.Tools.Core.Models;

/// <summary>
/// 工具 ViewModel 基类 - 提供工具类 VM 共享的剪贴板复制与状态/错误展示能力。
/// 状态消息 2 秒后自动清除（新消息会取消上一次的清除计时）。
/// 基于 CommunityToolkit.Mvvm 源生成器，AOT 兼容，无运行时反射。
/// </summary>
public abstract partial class ToolViewModelBase : ViewModelBase
{
    private CancellationTokenSource? _statusMessageCts;

    /// <summary>状态信息（成功时显示，2 秒后自动清除）</summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// 复制文本到剪贴板并显示成功消息；失败统一经 <see cref="ShowError"/> 呈现（服务层负责记录日志）
    /// </summary>
    /// <param name="clipboard">剪贴板服务</param>
    /// <param name="text">要复制的文本</param>
    /// <param name="successMessage">成功提示消息</param>
    protected async Task CopyToClipboardAsync(
        IClipboardService clipboard,
        string text,
        string successMessage = "✓ 已复制到剪贴板")
    {
        if (string.IsNullOrEmpty(text))
            return;

        try
        {
            await clipboard.SetTextAsync(text);
            ShowStatus(successMessage);
        }
        catch (Exception ex)
        {
            ShowError($"复制失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 显示成功状态消息（2 秒后自动清除；新消息取消上一次的清除计时）
    /// </summary>
    protected void ShowStatus(string message)
    {
        _statusMessageCts?.Cancel();
        _statusMessageCts?.Dispose();
        _statusMessageCts = new CancellationTokenSource();

        StatusMessage = message;
        _ = ClearStatusAfterDelayAsync(_statusMessageCts.Token);
    }

    /// <summary>
    /// 延迟清除状态消息（被取消时忽略——已有新消息就位）
    /// </summary>
    private async Task ClearStatusAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2), token);
            StatusMessage = string.Empty;
        }
        catch (OperationCanceledException)
        {
            // 新消息已就位，无需清除
        }
    }

    /// <summary>
    /// 显示错误（HasErrors/ErrorMessage/StatusMessage 三段式）。
    /// 子类可覆写以追加清理逻辑（如清空结果集合）。
    /// </summary>
    protected virtual void ShowError(string message)
    {
        HasErrors = true;
        ErrorMessage = message;
        StatusMessage = string.Empty;
    }

    /// <summary>
    /// 清除错误与状态消息
    /// </summary>
    protected void ClearError()
    {
        HasErrors = false;
        ErrorMessage = null;
        StatusMessage = string.Empty;
    }
}
