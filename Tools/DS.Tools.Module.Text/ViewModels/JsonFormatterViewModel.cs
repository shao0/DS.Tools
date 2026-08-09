using System.ComponentModel;
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
public sealed partial class JsonFormatterViewModel : ToolViewModelBase, ISubTool
{
    // 子工具元数据（ISubTool 静态抽象接口实现）：经 ToolRegistration.AddSubTool<T, TView>() 编译期读取注册
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

    /// <summary>
    /// 状态消息（基类属性）变化时刷新清空命令的可执行状态
    /// </summary>
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(StatusMessage))
        {
            ClearCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanExecuteFormat() => !string.IsNullOrWhiteSpace(InputJson) && !IsProcessing;

    private bool CanClear() =>
        !string.IsNullOrEmpty(InputJson) ||
        !string.IsNullOrEmpty(OutputJson) ||
        !string.IsNullOrEmpty(StatusMessage);

    private bool CanCopyOutput() => !string.IsNullOrWhiteSpace(OutputJson);

    /// <summary>
    /// 执行格式化（返回 Task 自动生成异步命令；CPU 操作移出 UI 线程——大数据量不冻结界面，spinner 真实可见）
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteFormat))]
    private Task FormatAsync() => ExecuteOperationAsync(() => _service.Format(InputJson));

    /// <summary>
    /// 执行压缩
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteFormat))]
    private Task CompressAsync() => ExecuteOperationAsync(() => _service.Compress(InputJson));

    /// <summary>
    /// 执行验证
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteFormat))]
    private Task ValidateAsync() => ExecuteOperationAsync(() => _service.Validate(InputJson));

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
    private Task CopyOutputAsync() => CopyToClipboardAsync(_clipboardService, OutputJson);

    /// <summary>
    /// 执行通用操作逻辑（CPU 操作经 Task.Run 移出 UI 线程，AsyncRelayCommand 自带防重入）
    /// </summary>
    private async Task ExecuteOperationAsync(Func<JsonFormatterResult> operation)
    {
        if (IsProcessing)
            return;

        ClearError();
        IsProcessing = true;

        try
        {
            var result = await Task.Run(operation);

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
            _logger.LogError(ex, "JSON 操作异常（{Operation}）", nameof(ExecuteOperationAsync));
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
            StatusMessage = $"✓ JSON 格式有效 | 深度: {result.JsonDepth} | 原始长度: {result.OriginalLength}";
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
    /// 显示错误（基类三段式基础上追加：清空输出区）
    /// </summary>
    protected override void ShowError(string message)
    {
        base.ShowError(message);
        HasOutput = false;
        OutputJson = string.Empty;
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
