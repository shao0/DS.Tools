using DS.Tools.Module.Base;
using Microsoft.Extensions.DependencyInjection;
using DS.Tools.Module.Text.ViewModels;
using DS.Tools.Module.Text.Services;
using System.Runtime.CompilerServices;

namespace DS.Tools.Module.Text;

/// <summary>
/// 文本工具模块实现 - 包含7个独立的子工具
/// 扩展友好：使用 SubToolManager 管理子工具
/// </summary>
public sealed class TextModule : ToolModule
{
    public override string Id => "text-tools";
    public override string Name => "文本工具";
    public override string Icon => "📝";
    public override string Description => "文本处理工具集，包含JSON格式化、Base64编码、颜色转换、密码生成等工具";

    /// <inheritdoc />
    /// <summary>
    /// 主 ViewModel 工厂：经 DI 容器按强类型解析（IoC，无 Type 键、无反射）
    /// </summary>
    public override ViewModelBase CreateMainViewModel(IServiceProvider services)
        => services.GetRequiredService<DashboardViewModel>();

    /// <summary>
    /// 构造函数 - 启用子工具支持
    /// </summary>
    public TextModule()
    {
        EnableSubTools();
        InitializeSubTools();
    }

    public override IServiceCollection Register(IServiceCollection services)
    {
        // 注册所有 ViewModel
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<JsonFormatterViewModel>();
        services.AddTransient<Base64ViewModel>();
        services.AddTransient<ColorConverterViewModel>();
        services.AddTransient<PasswordGeneratorViewModel>();
        services.AddTransient<TextHasherViewModel>();
        services.AddTransient<TimestampConverterViewModel>();

        // 注册共享服务
        services.AddSingleton<IJsonFormatterService, JsonFormatterService>();

        // 注意：ViewModel→View 映射不在此注册——
        // 由主应用 MainWindow.axaml 中的编译期 DataTemplate 声明（AOT 兼容，零反射）。

        return services;
    }

    public override void Initialize(IServiceProvider services)
    {
        // 模块初始化逻辑
    }

    /// <summary>
    /// 初始化子工具列表（扩展友好）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InitializeSubTools()
    {
        if (SubToolManager is null)
            return;

        // 使用扩展友好的 API 添加子工具
        // 工厂一律经 DI 容器创建（IoC）：编译期强类型，无 Type 键、无反射
        SubToolManager.AddSubTools(new[]
        {
            new SubToolInfo("dashboard", "仪表盘", "⏰", sp => sp.GetRequiredService<DashboardViewModel>()),
            new SubToolInfo("json-formatter", "JSON格式化", "📋", sp => sp.GetRequiredService<JsonFormatterViewModel>()),
            new SubToolInfo("base64-converter", "Base64编码", "🔐", sp => sp.GetRequiredService<Base64ViewModel>()),
            new SubToolInfo("color-converter", "颜色转换", "🎨", sp => sp.GetRequiredService<ColorConverterViewModel>()),
            new SubToolInfo("password-generator", "密码生成", "🔑", sp => sp.GetRequiredService<PasswordGeneratorViewModel>()),
            new SubToolInfo("text-hasher", "文本哈希", "🔒", sp => sp.GetRequiredService<TextHasherViewModel>()),
            new SubToolInfo("timestamp-converter", "时间戳转换", "⏰", sp => sp.GetRequiredService<TimestampConverterViewModel>())
        });
    }
}
