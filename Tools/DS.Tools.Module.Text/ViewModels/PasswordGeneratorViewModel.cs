using System.Security.Cryptography;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;


namespace DS.Tools.Module.Text.ViewModels;

/// <summary>
/// 密码生成器 ViewModel - 生成安全强密码
/// AOT 兼容，使用 RandomNumberGenerator（密码学安全）
/// </summary>
public sealed partial class PasswordGeneratorViewModel : ViewModelBase
{
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
    private void Copy()
    {
        if (!string.IsNullOrEmpty(GeneratedPassword))
        {
            // TODO: 实现剪贴板复制
            Console.WriteLine($"已复制密码: {GeneratedPassword}");
        }
    }

    private bool CanCopy() => !string.IsNullOrEmpty(GeneratedPassword);
}
