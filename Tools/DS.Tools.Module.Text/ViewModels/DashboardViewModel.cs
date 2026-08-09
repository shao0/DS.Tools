using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DS.Tools.Module.Base.Interfaces;
using Avalonia.Threading;


namespace DS.Tools.Module.Text.ViewModels;

/// <summary>
/// 仪表盘 ViewModel - 显示实时时间和快速操作
/// AOT 兼容，无反射调用
/// </summary>
public sealed partial class DashboardViewModel : ViewModelBase
{
    private readonly INavigationService? _navigationService;
    private readonly DispatcherTimer _clockTimer;

    /// <summary>当前时间（HH:mm 格式）</summary>
    [ObservableProperty]
    private string? _currentTime;

    /// <summary>当前日期（MM/dd 格式）</summary>
    [ObservableProperty]
    private string? _currentDate;

    /// <summary>当前时间戳（秒级）</summary>
    [ObservableProperty]
    private string? _timestamp;

    /// <summary>
    /// 构造函数
    /// </summary>
    public DashboardViewModel(INavigationService? navigationService = null)
    {
        _navigationService = navigationService;

        // 初始化 DispatcherTimer，每秒更新一次时钟
        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += OnClockTick;

        StartClock();
    }

    /// <summary>
    /// 启动时钟更新
    /// </summary>
    public void StartClock()
    {
        // 立即更新一次时钟
        UpdateClock();

        // 启动定时器
        if (!_clockTimer.IsEnabled)
        {
            _clockTimer.Start();
        }
    }

    /// <summary>
    /// 停止时钟更新
    /// </summary>
    public void StopClock()
    {
        if (_clockTimer.IsEnabled)
        {
            _clockTimer.Stop();
        }
    }

    /// <summary>
    /// 时钟定时器触发事件
    /// </summary>
    private void OnClockTick(object? sender, EventArgs e)
    {
        UpdateClock();
    }

    /// <summary>
    /// 更新时钟（每秒调用一次）
    /// </summary>
    private void UpdateClock()
    {
        var now = DateTime.Now;
        CurrentTime = now.ToString("HH:mm");
        CurrentDate = now.ToString("MM/dd");
        Timestamp = ((long)(now - DateTime.UnixEpoch).TotalSeconds).ToString();
    }

    /// <summary>导航到 JSON 格式化工具</summary>
    [RelayCommand]
    private void NavigateToJson()
    {
        _navigationService?.NavigateTo("text-tools:json-formatter");
    }

    /// <summary>导航到 Base64 工具</summary>
    [RelayCommand]
    private void NavigateToBase64()
    {
        _navigationService?.NavigateTo("text-tools:base64-converter");
    }

    /// <summary>导航到颜色转换工具</summary>
    [RelayCommand]
    private void NavigateToColor()
    {
        _navigationService?.NavigateTo("text-tools:color-converter");
    }

    /// <summary>导航到密码生成器</summary>
    [RelayCommand]
    private void NavigateToPassword()
    {
        _navigationService?.NavigateTo("text-tools:password-generator");
    }
}
