using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using DS.Tools.Core.Interfaces;
using DS.Tools.Module.Base.Interfaces;
using DS.Tools.Module.Text.Models;
using DS.Tools.Module.Text.Services;

namespace DS.Tools.Module.Text.ViewModels;

/// <summary>
/// JSON 格式化工具 ViewModel
/// 使用 CommunityToolkit.Mvvm 源生成器，AOT 兼容，无反射调用
/// </summary>
public sealed partial class JsonFormatterViewModel : ViewModelBase, ISubTool
{
    // 子工具元数据（ISubTool 静态抽象接口实现）：经 ToolRegistration.AddSubTool<T>() 编译期读取注册
    static string ISubTool.ModuleId => TextModule.ToolIds.Module;
    static string ISubTool.Id => TextModule.ToolIds.JsonFormatter;
    static string ISubTool.Name => "JSON格式化";
    static string ISubTool.Icon => "📋";

    private readonly IJsonFormatterService _service;
    private readonly IClipboardService _clipboardService;
    private readonly ILogger<JsonFormatterViewModel> _logger;

    /// <summary>
    /// 构造函数 —— 通过 DI 注入服务
    /// </summary>
    public JsonFormatterViewModel(
        IJsonFormatterService service,
        IClipboardService clipboardService,
        ILogger<JsonFormatterViewModel> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 输入 JSON 文本
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FormatCommand))]
    [NotifyCanExecuteChangedFor(nameof(CompressCommand))]
    [NotifyCanExecuteChangedFor(nameof(ValidateCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearCommand))]
    private string _inputJson = string.Empty;

    /// <summary>
    /// 输出 JSON 文本
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyOutputCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearCommand))]
    private string _outputJson = string.Empty;

    /// <summary>
    /// 状态信息（成功时显示统计）
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ClearCommand))]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// 是否正在处理
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FormatCommand))]
    [NotifyCanExecuteChangedFor(nameof(CompressCommand))]
    [NotifyCanExecuteChangedFor(nameof(ValidateCommand))]
    private bool _isProcessing;

    /// <summary>
    /// 是否有输出
    /// </summary>
    [ObservableProperty]
    private bool _hasOutput;

    /// <summary>
    /// 输入变化时清除错误与输出
    /// </summary>
    partial void OnInputJsonChanged(string value)
    {
        ClearError();
        ClearOutput();
    }

    private bool CanExecuteFormat() => !string.IsNullOrWhiteSpace(InputJson) && !IsProcessing;

    private bool CanClear() =>
        !string.IsNullOrEmpty(InputJson) ||
        !string.IsNullOrEmpty(OutputJson) ||
        !string.IsNullOrEmpty(StatusMessage);

    private bool CanCopyOutput() => !string.IsNullOrWhiteSpace(OutputJson);

    /// <summary>
    /// 执行格式化
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteFormat))]
    private void Format()
    {
        ExecuteOperation(() => _service.Format(InputJson));
    }

    /// <summary>
    /// 执行压缩
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteFormat))]
    private void Compress()
    {
        ExecuteOperation(() => _service.Compress(InputJson));
    }

    /// <summary>
    /// 执行验证
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteFormat))]
    private void Validate()
    {
        ExecuteOperation(() => _service.Validate(InputJson));
    }

    /// <summary>
    /// 执行清空
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanClear))]
    private void Clear()
    {
        InputJson = string.Empty;
        ClearError();
        ClearOutput();
    }

    /// <summary>
    /// 执行复制输出（异步，剪贴板需 UI 线程）
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCopyOutput))]
    private async Task CopyOutputAsync()
    {
        if (string.IsNullOrWhiteSpace(OutputJson))
            return;

        try
        {
            await _clipboardService.SetTextAsync(OutputJson);
            StatusMessage = "✓ 已复制到剪贴板";
            await Task.Delay(2000); // 显示成功消息后清除
            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            ShowError($"复制失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 执行通用操作逻辑（同步；数据量小，无阻塞风险）
    /// </summary>
    private void ExecuteOperation(Func<JsonFormatterResult> operation)
    {
        if (IsProcessing)
            return;

        ClearError();
        IsProcessing = true;

        try
        {
            var result = operation();

            if (result.IsSuccess)
            {
                ShowSuccess(result);
            }
            else
            {
                ShowError(result.ErrorMessage ?? "操作失败");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JSON 操作异常（{Operation}）", nameof(ExecuteOperation));
            ShowError($"操作异常: {ex.Message}");
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>
    /// 显示成功结果
    /// </summary>
    private void ShowSuccess(JsonFormatterResult result)
    {
        HasErrors = false;
        ErrorMessage = null;
        HasOutput = true;

        // 对于验证操作，不显示格式化的 JSON
        if (result.OperationType == JsonFormatterOperationType.Validate)
        {
            OutputJson = string.Empty;
            StatusMessage = $"✓ {result.FormattedJson} | 深度: {result.JsonDepth} | 原始长度: {result.OriginalLength}";
        }
        else
        {
            OutputJson = result.FormattedJson ?? string.Empty;
            var operationName = result.OperationType == JsonFormatterOperationType.Format ? "格式化" : "压缩";
            var sizeChange = result.ProcessedLength < result.OriginalLength
                ? $"减少了 {result.OriginalLength - result.ProcessedLength} 字符"
                : $"增加了 {result.ProcessedLength - result.OriginalLength} 字符";

            StatusMessage = $"✓ {operationName}成功 | {sizeChange} | 深度: {result.JsonDepth}";
        }
    }

    /// <summary>
    /// 显示错误
    /// </summary>
    private void ShowError(string message)
    {
        HasErrors = true;
        ErrorMessage = message;
        HasOutput = false;
        OutputJson = string.Empty;
        StatusMessage = string.Empty;
    }

    /// <summary>
    /// 清除错误状态
    /// </summary>
    private void ClearError()
    {
        HasErrors = false;
        ErrorMessage = null;
        StatusMessage = string.Empty;
    }

    /// <summary>
    /// 清除输出状态
    /// </summary>
    private void ClearOutput()
    {
        HasOutput = false;
        OutputJson = string.Empty;
    }
}
