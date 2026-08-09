using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Avalonia.Threading;
using DS.Tools.Core.Models;
using DS.Tools.Module.Base.Interfaces;

namespace DS.Tools.ViewModels;

/// <summary>
/// 主页 ViewModel - 显示实时时钟 + 按模块分组的功能入口（所有工具一览）。
/// 由 MainWindowViewModel 缓存复用（单实例），DispatcherTimer 不会泄漏。
/// AOT 兼容，无反射调用。
/// </summary>
public sealed partial class DashboardViewModel : ViewModelBase
{
    private readonly IToolRegistry _toolRegistry;
    private readonly INavigationService _navigationService;
    private readonly ILogger<DashboardViewModel> _logger;
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

    /// <summary>功能入口总数（统计卡片用）</summary>
    [ObservableProperty]
    private int _toolCount;

    /// <summary>
    /// 构造函数 - 构建按模块分组的功能列表并启动时钟
    /// </summary>
    public DashboardViewModel(
        IToolRegistry toolRegistry,
        INavigationService navigationService,
        ILogger<DashboardViewModel> logger)
    {
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        DisplayName = "主页";

        BuildModuleGroups();

        // 初始化 DispatcherTimer，每秒更新一次时钟
        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += OnClockTick;

        StartClock();
    }

    /// <summary>
    /// 按模块分组的功能入口（主页核心数据）
    /// </summary>
    public ObservableCollection<DashboardModuleGroup> ModuleGroups { get; } = [];

    /// <summary>
    /// 构建功能分组：遍历 ToolRegistry 的模块，子工具转为带完整导航 ID 的入口
    /// </summary>
    private void BuildModuleGroups()
    {
        ModuleGroups.Clear();
        ToolCount = 0;

        foreach (var module in _toolRegistry.Tools)
        {
            var items = module.SubTools?
                .Select(s => new DashboardToolItem(s.Icon, s.Name, s.GetFullNavigationId()))
                .ToList() ?? [];

            ToolCount += items.Count;
            ModuleGroups.Add(new DashboardModuleGroup(module.Icon, module.Name, items));
        }

        _logger.LogInformation("主页功能分组构建完成：模块 {ModuleCount} 个，功能 {ToolCount} 个", ModuleGroups.Count, ToolCount);
    }

    /// <summary>
    /// 导航到指定功能（完整导航 ID：module:subTool）
    /// </summary>
    [RelayCommand]
    private void NavigateToTool(string? navigationId)
    {
        if (string.IsNullOrWhiteSpace(navigationId))
            return;

        _navigationService.NavigateTo(navigationId);
    }

    /// <summary>
    /// 启动时钟更新（由 MainWindowViewModel 在导航到主页时调用；构造时已启动）
    /// </summary>
    internal void StartClock()
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
    /// 停止时钟更新（由 MainWindowViewModel 在导航离开主页时调用，避免计时器空转）
    /// </summary>
    internal void StopClock()
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
}

/// <summary>
/// 主页功能分组（对应一个工具模块）
/// </summary>
public sealed record DashboardModuleGroup(string ModuleIcon, string ModuleName, IReadOnlyList<DashboardToolItem> Tools);

/// <summary>
/// 主页功能入口（对应模块下的一个子工具，携带完整导航 ID）
/// </summary>
public sealed record DashboardToolItem(string Icon, string Name, string NavigationId);
