using System.Security.Cryptography;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;


namespace DS.Tools.Module.Text.ViewModels;

/// <summary>
/// 文本哈希 ViewModel - 计算文本的 MD5 和 SHA-256 哈希值
/// AOT 兼容，使用 SHA256.HashData 和 MD5.HashData 静态方法
/// </summary>
public sealed partial class TextHasherViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CalculateHashCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearCommand))]
    private string _inputText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopySha256Command))]
    [NotifyCanExecuteChangedFor(nameof(ClearCommand))]
    private string _sha256Result = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyMd5Command))]
    [NotifyCanExecuteChangedFor(nameof(ClearCommand))]
    private string _md5Result = string.Empty;

    [RelayCommand(CanExecute = nameof(CanCalculateHash))]
    private void CalculateHash()
    {
        if (string.IsNullOrWhiteSpace(InputText))
        {
            HasErrors = true;
            ErrorMessage = "请输入要计算哈希的文本";
            return;
        }

        try
        {
            var bytes = Encoding.UTF8.GetBytes(InputText);

            // 计算 SHA-256（AOT兼容的静态方法）
            var sha256Bytes = SHA256.HashData(bytes);
            Sha256Result = Convert.ToHexString(sha256Bytes).ToLowerInvariant();

            // 计算 MD5（AOT兼容的静态方法）
            var md5Bytes = MD5.HashData(bytes);
            Md5Result = Convert.ToHexString(md5Bytes).ToLowerInvariant();

            HasErrors = false;
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            HasErrors = true;
            ErrorMessage = $"计算失败: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanClear))]
    private void Clear()
    {
        InputText = string.Empty;
        Sha256Result = string.Empty;
        Md5Result = string.Empty;
        HasErrors = false;
        ErrorMessage = null;
    }

    [RelayCommand(CanExecute = nameof(CanCopySha256))]
    private void CopySha256()
    {
        if (!string.IsNullOrEmpty(Sha256Result))
        {
            // TODO: 实现剪贴板复制
            Console.WriteLine($"已复制 SHA-256: {Sha256Result}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanCopyMd5))]
    private void CopyMd5()
    {
        if (!string.IsNullOrEmpty(Md5Result))
        {
            // TODO: 实现剪贴板复制
            Console.WriteLine($"已复制 MD5: {Md5Result}");
        }
    }

    private bool CanCalculateHash() => !string.IsNullOrWhiteSpace(InputText);

    private bool CanClear() => !string.IsNullOrEmpty(InputText) ||
                                !string.IsNullOrEmpty(Sha256Result) ||
                                !string.IsNullOrEmpty(Md5Result);

    private bool CanCopySha256() => !string.IsNullOrEmpty(Sha256Result);

    private bool CanCopyMd5() => !string.IsNullOrEmpty(Md5Result);
}
