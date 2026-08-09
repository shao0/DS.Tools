using System.Security.Cryptography;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DS.Tools.Core.Interfaces;
using DS.Tools.Module.Base.Interfaces;


namespace DS.Tools.Module.Text.ViewModels;

/// <summary>
/// 密码生成器 ViewModel - 生成安全强密码
/// AOT 兼容，使用 RandomNumberGenerator（密码学安全）
/// </summary>
public sealed partial class PasswordGeneratorViewModel : ViewModelBase, ISubTool
{
    // 子工具元数据（ISubTool 静态抽象接口实现）：经 ToolRegistration.AddSubTool<T>() 编译期读取注册
    static string ISubTool.ModuleId => TextModule.ToolIds.Module;
    static string ISubTool.Id => TextModule.ToolIds.PasswordGenerator;
    static string ISubTool.Name => "密码生成";
    static string ISubTool.Icon => "🔑";

    private readonly IClipboardService _clipboardService;

    /// <summary>
    /// 构造函数 - 显式依赖注入
    /// </summary>
    public PasswordGeneratorViewModel(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
    }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PasswordLengthDisplay))]
    private int _passwordLength = 16;

    [ObservableProperty]
    private bool _useUppercase = true;

    [ObservableProperty]
    private bool _useLowercase = true;

    [ObservableProperty]
    private bool _useNumbers = true;

    [ObservableProperty]
    private bool _useSymbols = false;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyCommand))]
    private string _generatedPassword = string.Empty;

    /// <summary>密码长度显示文本（只读计算属性）</summary>
    public string PasswordLengthDisplay => PasswordLength.ToString();

    [RelayCommand]
    private void Generate()
    {
        const string upperChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lowerChars = "abcdefghijklmnopqrstuvwxyz";
        const string numberChars = "0123456789";
        const string symbolChars = "!@#$%^&*()_+-=[]{}|;:,.<>?";

        var charSet = new StringBuilder();

        if (UseUppercase) charSet.Append(upperChars);
        if (UseLowercase) charSet.Append(lowerChars);
        if (UseNumbers) charSet.Append(numberChars);
        if (UseSymbols) charSet.Append(symbolChars);

        if (charSet.Length == 0)
        {
            HasErrors = true;
            ErrorMessage = "请至少选择一种字符类型";
            GeneratedPassword = string.Empty;
            return;
        }

        var chars = charSet.ToString();
        var result = new char[PasswordLength];

        // 使用密码学安全的随机数生成器
        using var rng = RandomNumberGenerator.Create();
        var byteArray = new byte[PasswordLength * 4];

        rng.GetBytes(byteArray);

        for (var i = 0; i < PasswordLength; i++)
        {
            var uintValue = BitConverter.ToUInt32(byteArray, i * 4);
            var index = (int)(uintValue % (uint)chars.Length);
            result[i] = chars[index];
        }

        GeneratedPassword = new string(result);
        HasErrors = false;
        ErrorMessage = null;
    }

    [RelayCommand(CanExecute = nameof(CanCopy))]
    private async Task CopyAsync()
    {
        if (!string.IsNullOrEmpty(GeneratedPassword))
        {
            try
            {
                await _clipboardService.SetTextAsync(GeneratedPassword);
                HasErrors = false;
                ErrorMessage = null;
            }
            catch (Exception ex)
            {
                HasErrors = true;
                ErrorMessage = $"复制失败: {ex.Message}";
            }
        }
    }

    private bool CanCopy() => !string.IsNullOrEmpty(GeneratedPassword);
}
