using System.Collections.ObjectModel;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using DS.Tools.Core.Interfaces;
using DS.Tools.Core.Models;
using DS.Tools.Module.Base;
using DS.Tools.Module.Base.Interfaces;

namespace DS.Tools.ViewModels;

/// <summary>
/// 主窗口 ViewModel - 管理工具导航和布局。
/// 支持二级菜单：一级为模块，二级为子工具。
/// ViewModel 按 (模块, 子工具) 导航键缓存复用：切换工具保留状态，避免重复创建（如 Dashboard 时钟）。
/// 基于 CommunityToolkit.Mvvm（源生成器），NativeAOT 兼容。
/// </summary>
public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly IToolRegistry _toolRegistry;
    private readonly IThemeService _themeService;
    private readonly INavigationService _navigationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<(string ToolId, string? SubToolId), ViewModelBase> _viewModelCache = [];

    /// <summary>主页在 ViewModel 缓存中的保留键（主页为应用级，不属于任何模块）</summary>
    private const string HomeCacheKey = "__home__";

    /// <summary>
    /// 构造函数 - 显式依赖注入
    /// </summary>
    public MainWindowViewModel(
        IToolRegistry toolRegistry,
        IThemeService themeService,
        INavigationService navigationService,
        IServiceProvider serviceProvider)
    {
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        // 初始化工具列表
        Tools = [.. _toolRegistry.Tools];

        // 订阅导航变更事件
        _navigationService.NavigationChanged += OnNavigationChanged;

        // 设置默认工具
        NavigateToDefaultTool();

        // 初始化主题图标
        UpdateThemeIcon();
    }

    /// <summary>
    /// 工具列表（一级菜单）
    /// </summary>
    public ObservableCollection<IToolModule> Tools { get; }

    /// <summary>
    /// 当前活动的工具 ViewModel
    /// </summary>
    [ObservableProperty] private ViewModelBase? _activeToolViewModel;

    /// <summary>
    /// 当前主题图标
    /// </summary>
    [ObservableProperty] private string _currentThemeIcon = "🌙";

    /// <summary>
    /// 侧边栏是否打开
    /// </summary>
    [ObservableProperty] private bool _isPaneOpen;

    /// <summary>
    /// 导航到默认工具（主页）
    /// </summary>
    private void NavigateToDefaultTool() => NavigateHome();

    /// <summary>
    /// 导航到主页：应用级 DashboardViewModel，经 DI 创建并缓存复用（与模块导航同缓存策略）
    /// </summary>
    private void NavigateHome()
    {
        var key = (HomeCacheKey, (string?)null);
        if (!_viewModelCache.TryGetValue(key, out var viewModel))
        {
            viewModel = _serviceProvider.GetRequiredService<DashboardViewModel>();
            _viewModelCache[key] = viewModel;
        }

        ActiveToolViewModel = viewModel;
    }

    /// <summary>
    /// 返回主页命令（左上角图标触发）
    /// </summary>
    [RelayCommand]
    private void NavigateToHome() => NavigateHome();

    /// <summary>
    /// 导航变更回调 - 按导航键缓存并复用 ViewModel 实例
    /// </summary>
    private void OnNavigationChanged(IToolModule? tool, string? subToolId)
    {
        if (tool is null)
            return;

        var key = (tool.Id, subToolId);
        if (!_viewModelCache.TryGetValue(key, out var viewModel))
        {
            // IoC 创建：ViewModel 由模块提供的强类型工厂经 DI 容器解析
            // （编译期泛型 GetRequiredService<T>，无 Type 键、无运行时反射）
            viewModel = string.IsNullOrEmpty(subToolId)
                ? tool.CreateMainViewModel(_serviceProvider)
                : tool.CreateSubToolViewModel(subToolId, _serviceProvider) ?? tool.CreateMainViewModel(_serviceProvider);

            _viewModelCache[key] = viewModel;
        }

        ActiveToolViewModel = viewModel;
    }

    /// <summary>
    /// 选择子工具命令（二级菜单）
    /// 注意：子工具按钮可能来自任意模块（侧边栏按模块分组展开），
    /// 导航 ID 必须以子工具所属模块为准，不能假定当前活动模块（多模块下会导致错误路由）。
    /// </summary>
    [RelayCommand]
    private void SelectSubTool(object? parameter)
    {
        if (parameter is not SubToolInfo subTool)
            return;

        // 从注册表解析拥有该子工具的模块（SubToolInfo 引用相等即可匹配）
        var ownerModule = _toolRegistry.Tools.FirstOrDefault(m => m.SubTools?.Contains(subTool) == true);
        if (ownerModule is null)
            return;

        var fullNavigationId = subTool.GetFullNavigationId(ownerModule.Id);
        _navigationService.NavigateTo(fullNavigationId);
    }

    /// <summary>
    /// 切换侧边栏命令
    /// </summary>
    [RelayCommand]
    private void TogglePane()
    {
        IsPaneOpen = !IsPaneOpen;
    }

    /// <summary>
    /// 切换主题命令
    /// </summary>
    [RelayCommand]
    private void ToggleTheme()
    {
        var newTheme = _themeService.CurrentTheme == ThemeVariant.Dark
            ? ThemeVariant.Light
            : ThemeVariant.Dark;

        _themeService.SetTheme(newTheme);
        UpdateThemeIcon();
    }

    /// <summary>
    /// 更新主题图标
    /// </summary>
    private void UpdateThemeIcon()
    {
        // 根据当前主题设置图标
        CurrentThemeIcon = _themeService.CurrentTheme == ThemeVariant.Dark ? "☀️" : "🌙";
    }
}
