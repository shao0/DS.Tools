using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DS.Tools.Module.Base.Interfaces;


namespace DS.Tools.Module.Text.ViewModels;

/// <summary>
/// 时间戳转换 ViewModel - 时间戳与日期时间相互转换
/// AOT 兼容，使用 DateTimeOffset.FromUnixTime*
/// </summary>
public sealed partial class TimestampConverterViewModel : ViewModelBase, ISubTool
{
    // 子工具元数据（ISubTool 静态抽象接口实现）：经 ToolRegistration.AddSubTool<T, TView>() 编译期读取注册
    static string ISubTool.ModuleId => TextModule.ToolIds.Module;
    static string ISubTool.Id => TextModule.ToolIds.TimestampConverter;
    static string ISubTool.Name => "时间戳转换";
    static string ISubTool.Icon => "⏰";

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

        // InvariantCulture：输入格式与区域无关（zh-CN/en-US 解析行为一致）
        if (!DateTime.TryParse(DateInput.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
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

    private bool CanTimestampToDate() => !string.IsNullOrWhiteSpace(TimestampInput);

    private bool CanDateToTimestamp() => !string.IsNullOrWhiteSpace(DateInput);

    private bool CanClearTimestamp() => !string.IsNullOrEmpty(TimestampInput) ||
                                          !string.IsNullOrEmpty(ConvertedDateTime);

    private bool CanClearDate() => !string.IsNullOrEmpty(DateInput) ||
                                     !string.IsNullOrEmpty(SecondsTimestamp);
}
