using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DS.Tools.Module.Base.Interfaces;

namespace DS.Tools.Module.Text.ViewModels;

/// <summary>
/// 仪表盘 ViewModel - 显示实时时间和快速操作
/// AOT 兼容，无反射调用
/// </summary>
public sealed partial class DashboardViewModel : ViewModelBase
{
    private readonly INavigationService? _navigationService;

    /// <summary>当前时间（HH:mm 格式）</summary>
    [ObservableProperty]
    private string? _currentTime;

    /// <summary>当前日期（MM/dd 格式）</summary>
    [ObservableProperty]
    private string? _currentDate;

    /// <summary>当前时间戳（秒级）</summary>
    [ObservableProperty]
    private string? _timestamp;

    /// <summary>时钟是否正在运行（内部控制标志，非绑定属性）</summary>
    private bool _isClockRunning;

    /// <summary>
    /// 构造函数
    /// </summary>
    public DashboardViewModel(INavigationService? navigationService = null)
    {
        _navigationService = navigationService;
        StartClock();
    }

    /// <summary>
    /// 启动时钟更新
    /// </summary>
    public void StartClock()
    {
        if (_isClockRunning)
            return;

        _isClockRunning = true;
        UpdateClock();

        // TODO: 在实际应用中，应该使用 DispatcherTimer 实现
        // 这里只是演示，实际需要使用 Avalonia 的定时器机制
        // 暂时通过手动更新模拟时钟运行
    }

    /// <summary>
    /// 停止时钟更新
    /// </summary>
    public void StopClock()
    {
        _isClockRunning = false;
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
