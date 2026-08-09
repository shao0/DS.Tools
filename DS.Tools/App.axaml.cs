using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DS.Tools.Module.Base.Interfaces;
using DS.Tools.Core.DI;
using DS.Tools.Core.Interfaces;
using DS.Tools.Module.Base.DI;
using DS.Tools.Module.Base.UI;
using DS.Tools.Views;
using DS.Tools.ViewModels;
using DS.Tools.Module.Text;
using System.Runtime.CompilerServices;

namespace DS.Tools;

/// <summary>
/// 应用程序入口 - 基于极简模块化架构（IToolModule + ToolRegistry + INavigationService）。
/// 模块在编译期显式注册，由IServiceProvider管理生命周期。
/// NativeAOT兼容，无运行时反射。
/// </summary>
public partial class App : Application
{
    private IServiceProvider? _serviceProvider;

    /// <summary>
    /// 应用程序初始化
    /// </summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// 框架初始化完成后：模块化注册 + 服务构建 + 主窗口启动
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        // ===== 阶段1：配置 + 服务集合 =====
        var configuration = BuildConfiguration();
        var services = ConfigureServices(configuration);

        // ===== 阶段2：注册工具模块（编译期显式）=====
        RegisterToolModules(services);

        // ===== 阶段3：Build 容器 =====
        _serviceProvider = services.BuildServiceProvider();

        // ===== 阶段4：初始化工具模块 =====
        InitializeToolModules();

        // ===== 阶段5：主题设置 =====
        ApplyThemeSettings(_serviceProvider.GetRequiredService<IThemeService>(), configuration);

        base.OnFrameworkInitializationCompleted();

        // ===== 阶段6：主窗口启动 =====
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.DataContext = _serviceProvider.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = mainWindow;
        }
    }

    /// <summary>
    /// 构建配置（AOT 兼容）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IConfiguration BuildConfiguration()
    {
        var configurationBuilder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

        return configurationBuilder.Build();
    }

    /// <summary>
    /// 配置服务（显式注册，AOT 兼容，禁止反射扫描）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IServiceCollection ConfigureServices(IConfiguration configuration)
    {
        var services = new ServiceCollection();

        // 添加配置
        services.AddSingleton(configuration);

        // 添加日志服务
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // 添加核心服务（显式注册，AOT 兼容）
        services.AddCoreServices();
        services.AddApplicationServices();
        services.AddModuleServices();

        // 注册 ViewModel 和 View
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<MainWindow>();

        return services;
    }

    /// <summary>
    /// 注册工具模块（编译期显式注册，无反射）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RegisterToolModules(IServiceCollection services)
    {
        // 实例化文本工具模块
        var textModule = new TextModule();

        // 注册模块的服务到 DI 容器
        textModule.Register(services);

        // 将模块本身注册为临时服务，以便后续初始化
        services.AddSingleton(textModule);
    }

    /// <summary>
    /// 初始化工具模块（在容器构建后）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InitializeToolModules()
    {
        if (_serviceProvider is null)
            return;

        // 从容器获取已注册的模块实例
        var textModule = _serviceProvider.GetRequiredService<TextModule>();

        // 获取 ToolRegistry 并注册模块
        var toolRegistry = _serviceProvider.GetRequiredService<IToolRegistry>();
        toolRegistry.Register(textModule);

        // 初始化模块
        textModule.Initialize(_serviceProvider);
    }

    /// <summary>
    /// 应用主题设置
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ApplyThemeSettings(IThemeService themeService, IConfiguration configuration)
    {
        // 从配置读取默认主题
        var defaultTheme = configuration["Theme:DefaultTheme"] ?? "System";

        // 应用主题
        var theme = defaultTheme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            "System" => ThemeVariant.Default,
            _ => ThemeVariant.Default
        };

        themeService.SetTheme(theme);
    }
}