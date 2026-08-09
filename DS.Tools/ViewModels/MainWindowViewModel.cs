using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DS.Tools.Module.Base.Interfaces;
using DS.Tools.Module.Base;
using DS.Tools.Core.Interfaces;
using DS.Tools.Core.Models;

namespace DS.Tools.ViewModels;

/// <summary>
/// 主窗口 ViewModel - 管理工具导航和布局
/// 支持二级菜单：一级为模块，二级为子工具，支持展开/收起功能
/// 基于 CommunityToolkit.Mvvm（源生成器），NativeAOT 兼容
/// </summary>
public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly IToolRegistry _toolRegistry;
    private readonly IThemeService _themeService;
    private readonly INavigationService _navigationService;
    private readonly IServiceProvider _serviceProvider;

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
        Tools = new ObservableCollection<IToolModule>(_toolRegistry.Tools);

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
    [ObservableProperty] private bool _isPaneOpen = true;

    /// <summary>
    /// 导航到默认工具
    /// </summary>
    private void NavigateToDefaultTool()
    {
        var defaultTool = _toolRegistry.GetTool("text-tools");
        if (defaultTool is not null)
        {
            // 导航到第一个子工具
            _navigationService.NavigateTo("text-tools:dashboard");
        }
    }

    /// <summary>
    /// 导航变更回调
    /// </summary>
    private void OnNavigationChanged(IToolModule? tool, string? subToolId)
    {
        if (tool is not null)
        {
            Type viewModelType;

            // 如果有子工具ID，则从模块的子工具列表中查找对应的ViewModel类型
            if (!string.IsNullOrEmpty(subToolId))
            {
                viewModelType = GetSubToolViewModelType(tool, subToolId);
            }
            else
            {
                // 否则使用模块的主ViewModel类型
                viewModelType = tool.ViewModelType;
            }

            // 从DI容器解析对应的ViewModel
            var viewModel = _serviceProvider.GetService(viewModelType) as ViewModelBase;
            ActiveToolViewModel = viewModel;
        }
    }

    /// <summary>
    /// 从工具模块获取子工具的ViewModel类型（AOT兼容）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Type GetSubToolViewModelType(IToolModule tool, string subToolId)
    {
        // 使用模块接口的 GetSubToolViewModelType 方法（AOT兼容）
        var viewModelType = tool.GetSubToolViewModelType(subToolId);

        // 如果找不到子工具，回退到主ViewModel
        return viewModelType ?? tool.ViewModelType;
    }

    /// <summary>
    /// 选择子工具命令（二级菜单）
    /// </summary>
    [RelayCommand]
    private void SelectSubTool(object? parameter)
    {
        if (parameter is SubToolInfo subTool && _navigationService.CurrentTool is not null)
        {
            var module = _navigationService.CurrentTool;
            var fullNavigationId = subTool.GetFullNavigationId(module.Id);
            _navigationService.NavigateTo(fullNavigationId);
        }
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
