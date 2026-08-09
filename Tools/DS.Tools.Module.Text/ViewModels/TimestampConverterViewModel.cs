using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;


namespace DS.Tools.Module.Text.ViewModels;

/// <summary>
/// 时间戳转换 ViewModel - 时间戳与日期时间相互转换
/// AOT 兼容，使用 DateTimeOffset.FromUnixTime*
/// </summary>
public sealed partial class TimestampConverterViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TimestampToDateCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearTimestampCommand))]
    private string _timestampInput = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ClearTimestampCommand))]
    private string _convertedDateTime = string.Empty;

    [ObservableProperty]
    private string _utcDateTime = string.Empty;

    [ObservableProperty]
    private string _isoFormat = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DateToTimestampCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearDateCommand))]
    private string _dateInput = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ClearDateCommand))]
    private string _secondsTimestamp = string.Empty;

    [ObservableProperty]
    private string _millisecondsTimestamp = string.Empty;

    [RelayCommand(CanExecute = nameof(CanTimestampToDate))]
    private void TimestampToDate()
    {
        if (string.IsNullOrWhiteSpace(TimestampInput))
        {
            HasErrors = true;
            ErrorMessage = "请输入时间戳";
            return;
        }

        if (!long.TryParse(TimestampInput.Trim(), out var timestamp))
        {
            HasErrors = true;
            ErrorMessage = "无效的时间戳格式";
            return;
        }

        try
        {
            // 自动检测秒级或毫秒级（13位以上为毫秒级）
            var dateTime = timestamp < 10000000000
                ? DateTimeOffset.FromUnixTimeSeconds(timestamp)
                : DateTimeOffset.FromUnixTimeMilliseconds(timestamp);

            ConvertedDateTime = dateTime.ToString("yyyy-MM-dd HH:mm:ss");
            UtcDateTime = dateTime.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss UTC");
            IsoFormat = dateTime.ToString("o");

            HasErrors = false;
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            HasErrors = true;
            ErrorMessage = $"转换失败: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanDateToTimestamp))]
    private void DateToTimestamp()
    {
        if (string.IsNullOrWhiteSpace(DateInput))
        {
            HasErrors = true;
            ErrorMessage = "请输入日期时间";
            return;
        }

        if (!DateTime.TryParse(DateInput.Trim(), out var dateTime))
        {
            HasErrors = true;
            ErrorMessage = "无效的日期格式，请使用 yyyy-MM-dd HH:mm:ss 格式";
            return;
        }

        try
        {
            var offset = new DateTimeOffset(dateTime);
            SecondsTimestamp = offset.ToUnixTimeSeconds().ToString();
            MillisecondsTimestamp = offset.ToUnixTimeMilliseconds().ToString();

            HasErrors = false;
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            HasErrors = true;
            ErrorMessage = $"转换失败: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanClearTimestamp))]
    private void ClearTimestamp()
    {
        TimestampInput = string.Empty;
        ConvertedDateTime = string.Empty;
        UtcDateTime = string.Empty;
        IsoFormat = string.Empty;
        HasErrors = false;
        ErrorMessage = null;
    }

    [RelayCommand(CanExecute = nameof(CanClearDate))]
    private void ClearDate()
    {
        DateInput = string.Empty;
        SecondsTimestamp = string.Empty;
        MillisecondsTimestamp = string.Empty;
        HasErrors = false;
        ErrorMessage = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CanTimestampToDate() => !string.IsNullOrWhiteSpace(TimestampInput);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CanDateToTimestamp() => !string.IsNullOrWhiteSpace(DateInput);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CanClearTimestamp() => !string.IsNullOrEmpty(TimestampInput) ||
                                          !string.IsNullOrEmpty(ConvertedDateTime);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CanClearDate() => !string.IsNullOrEmpty(DateInput) ||
                                     !string.IsNullOrEmpty(SecondsTimestamp);
}
