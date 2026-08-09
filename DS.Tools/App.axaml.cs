using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using DS.Tools.Core.DI;
using DS.Tools.Core.Interfaces;
using DS.Tools.Infrastructure.Logging;
using DS.Tools.Module.Base.DI;
using DS.Tools.Module.Base.Interfaces;
using DS.Tools.Module.Base.Services;
using DS.Tools.Module.Git;
using DS.Tools.Module.Text;
using DS.Tools.ViewModels;
using DS.Tools.Views;

namespace DS.Tools;

/// <summary>
/// 应用程序入口 - 基于极简模块化架构（IToolModule + ToolRegistry + INavigationService）。
/// 模块在编译期显式注册（<see cref="ToolModules"/> 数组），由 IServiceProvider 管理生命周期。
/// NativeAOT 兼容，无运行时反射。
/// </summary>
public partial class App : Application
{
    private IServiceProvider? _serviceProvider;

    /// <summary>
    /// 工具模块清单（编译期显式声明——新增模块只需在此追加一行）
    /// </summary>
    private static readonly IToolModule[] ToolModules = [new TextModule(), new GitModule()];

    /// <summary>
    /// 应用程序初始化
    /// </summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// 框架初始化完成后：配置 + 服务构建 + 模块注册/初始化 + 主窗口启动
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

        var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
        // 程序集版本仅元数据访问（非类型反射），AOT/Trim 安全
        var appVersion = typeof(App).Assembly.GetName().Version?.ToString() ?? "1.0.0";
        logger.LogInformation("应用启动：{AppName} v{AppVersion}，工具模块 {ModuleCount} 个", "DS.Tools", appVersion, ToolModules.Length);

        // 将 Avalonia 内部日志（含绑定错误）接入 ILogger，便于排查 UI 绑定问题
        Avalonia.Logging.Logger.Sink = new AvaloniaLogSink(logger);

        // ===== 阶段4：初始化工具模块（注册到 ToolRegistry）=====
        InitializeToolModules(logger);

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
    private IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();
    }

    /// <summary>
    /// 配置服务（显式注册，AOT 兼容，禁止反射扫描）
    /// </summary>
    private IServiceCollection ConfigureServices(IConfiguration configuration)
    {
        var services = new ServiceCollection();

        // 添加配置
        services.AddSingleton(configuration);

        // 添加日志服务（Serilog 实现，级别从 appsettings.json 读取）
        services.AddLogging(builder =>
        {
            builder.AddSerilog(SerilogConfig.CreateLogger(configuration), dispose: true);
        });

        // 添加核心服务（显式注册，AOT 兼容）
        services.AddApplicationServices();
        services.AddModuleServices();

        // 注册 ViewModel 和 View（主页 View 经 RegisterToolModules 的 viewMappings.Add 注册）
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<DashboardViewModel>(); // 主页（应用级，经 DI 创建）
        services.AddTransient<MainWindow>();

        return services;
    }

    /// <summary>
    /// 注册工具模块：模块服务入 DI 容器 + 模块实例注册为单例
    /// （编译期显式，无反射；模块 Register 一个方法完成子工具/View 映射/服务全部注册，
    /// 经 ToolRegistration.AddSubTool/AddViewMapping 扩展方法入容器）
    /// </summary>
    private void RegisterToolModules(IServiceCollection services)
    {
        // 主页（应用级，不属于任何模块）的 View 映射在组合根注册
        services.AddViewMapping<DashboardViewModel, DashboardView>();

        foreach (var module in ToolModules)
        {
            module.Register(services);
            services.AddSingleton(module);
        }
    }

    /// <summary>
    /// 初始化工具模块（在容器构建后）：注册到 ToolRegistry + 调用模块 Initialize
    /// </summary>
    private void InitializeToolModules(ILogger<App> logger)
    {
        if (_serviceProvider is null)
            return;

        var toolRegistry = _serviceProvider.GetRequiredService<IToolRegistry>();

        foreach (var module in ToolModules)
        {
            toolRegistry.Register(module);
            module.Initialize(_serviceProvider);
            logger.LogInformation("模块 {ModuleId} 已注册并初始化", module.Id);
        }
    }

    /// <summary>
    /// 应用主题设置（从配置读取默认主题）
    /// </summary>
    private void ApplyThemeSettings(IThemeService themeService, IConfiguration configuration)
    {
        var defaultTheme = configuration["Theme:DefaultTheme"] ?? "System";

        var theme = defaultTheme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };

        themeService.SetTheme(theme);
    }
}
