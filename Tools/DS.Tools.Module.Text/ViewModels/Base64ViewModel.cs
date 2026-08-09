using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DS.Tools.Core.Interfaces;
using DS.Tools.Module.Base.Interfaces;

namespace DS.Tools.Module.Text.ViewModels;

/// <summary>
/// Base64 编解码 ViewModel - 文本与 Base64 之间的相互转换
/// 基于 CommunityToolkit.Mvvm（源生成器），NativeAOT 兼容，无运行时反射
/// </summary>
public sealed partial class Base64ViewModel : ToolViewModelBase, ISubTool
{
    // 子工具元数据（ISubTool 静态抽象接口实现）：经 ToolRegistration.AddSubTool<T>() 编译期读取注册
    static string ISubTool.ModuleId => TextModule.ToolIds.Module;
    static string ISubTool.Id => TextModule.ToolIds.Base64Converter;
    static string ISubTool.Name => "Base64编码";
    static string ISubTool.Icon => "🔐";

    private readonly IClipboardService _clipboardService;

    /// <summary>
    /// 构造函数 - 显式依赖注入
    /// </summary>
    public Base64ViewModel(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
    }
    /// <summary>
    /// 输入文本 - 变更时自动刷新相关命令的 CanExecute 状态
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EncodeCommand))]
    [NotifyCanExecuteChangedFor(nameof(DecodeCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearCommand))]
    private string _inputText = string.Empty;

    /// <summary>
    /// 编码结果 - 变更时自动刷新复制和清空命令
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyEncodedCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearCommand))]
    private string _encodedResult = string.Empty;

    /// <summary>
    /// 解码结果 - 变更时自动刷新复制和清空命令
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyDecodedCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearCommand))]
    private string _decodedResult = string.Empty;

    /// <summary>
    /// 执行编码（文本 → Base64）
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanEncode))]
    private void Encode()
    {
        if (string.IsNullOrEmpty(InputText))
        {
            HasErrors = true;
            ErrorMessage = "请输入要编码的文本";
            return;
        }

        try
        {
            var bytes = Encoding.UTF8.GetBytes((string)InputText);
            EncodedResult = Convert.ToBase64String(bytes);
            HasErrors = false;
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            HasErrors = true;
            ErrorMessage = $"编码失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 执行解码（Base64 → 文本）
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDecode))]
    private void Decode()
    {
        if (string.IsNullOrEmpty(InputText))
        {
            HasErrors = true;
            ErrorMessage = "请输入 Base64 字符串";
            return;
        }

        try
        {
            var bytes = Convert.FromBase64String(InputText);
            DecodedResult = Encoding.UTF8.GetString(bytes);
            HasErrors = false;
            ErrorMessage = null;
        }
        catch (FormatException)
        {
            HasErrors = true;
            ErrorMessage = "无效的 Base64 字符串";
        }
        catch (Exception ex)
        {
            HasErrors = true;
            ErrorMessage = $"解码失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 清空所有输入和输出
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanClear))]
    private void Clear()
    {
        InputText = string.Empty;
        EncodedResult = string.Empty;
        DecodedResult = string.Empty;
        HasErrors = false;
        ErrorMessage = null;
    }

    /// <summary>
    /// 复制编码结果到剪贴板
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCopyEncoded))]
    private Task CopyEncodedAsync() => CopyToClipboardAsync(_clipboardService, EncodedResult);

    /// <summary>
    /// 复制解码结果到剪贴板
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCopyDecoded))]
    private Task CopyDecodedAsync() => CopyToClipboardAsync(_clipboardService, DecodedResult);

    private bool CanEncode() => !string.IsNullOrEmpty(InputText);
    private bool CanDecode() => !string.IsNullOrEmpty(InputText);
    private bool CanClear() =>
        !string.IsNullOrEmpty(InputText) ||
        !string.IsNullOrEmpty(EncodedResult) ||
        !string.IsNullOrEmpty(DecodedResult);
    private bool CanCopyEncoded() => !string.IsNullOrEmpty(EncodedResult);
    private bool CanCopyDecoded() => !string.IsNullOrEmpty(DecodedResult);
}
