using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DS.Tools.Module.Base;
using DS.Tools.Module.Text.Services;
using DS.Tools.Module.Text.ViewModels;

namespace DS.Tools.Module.Text;

/// <summary>
/// 文本工具模块 - 包含多个独立的子工具。
/// 扩展友好：使用 SubToolManager 管理子工具，ViewModel 经 DI 容器按强类型解析（IoC，无 Type 键、无反射）。
/// </summary>
public sealed class TextModule : ToolModule
{
    /// <summary>
    /// 文本模块及子工具的导航 ID 常量（全模块唯一引用点，避免魔法字符串散落）
    /// </summary>
    public static class ToolIds
    {
        /// <summary>模块 ID</summary>
        public const string Module = "text-tools";

        /// <summary>子工具：仪表盘</summary>
        public const string Dashboard = "dashboard";

        /// <summary>子工具：JSON 格式化</summary>
        public const string JsonFormatter = "json-formatter";

        /// <summary>子工具：Base64 编解码</summary>
        public const string Base64Converter = "base64-converter";

        /// <summary>子工具：颜色转换</summary>
        public const string ColorConverter = "color-converter";

        /// <summary>子工具：密码生成</summary>
        public const string PasswordGenerator = "password-generator";

        /// <summary>子工具：文本哈希</summary>
        public const string TextHasher = "text-hasher";

        /// <summary>子工具：时间戳转换</summary>
        public const string TimestampConverter = "timestamp-converter";

        /// <summary>完整导航 ID（module:subTool）</summary>
        public static string Full(string subToolId) => $"{Module}:{subToolId}";
    }

    public override string Id => ToolIds.Module;
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
        var logger = services.GetRequiredService<ILogger<TextModule>>();
        logger.LogInformation("模块 {ModuleId} 初始化完成，子工具 {SubToolCount} 个", Id, SubTools?.Count ?? 0);
    }

    /// <summary>
    /// 初始化子工具列表（扩展友好）
    /// </summary>
    private void InitializeSubTools()
    {
        if (SubToolManager is null)
            return;

        // 工厂一律经 DI 容器创建（IoC）：编译期强类型，无 Type 键、无反射
        SubToolManager.AddSubTools(
        [
            new SubToolInfo(ToolIds.Dashboard, "仪表盘", "⏰", sp => sp.GetRequiredService<DashboardViewModel>()),
            new SubToolInfo(ToolIds.JsonFormatter, "JSON格式化", "📋", sp => sp.GetRequiredService<JsonFormatterViewModel>()),
            new SubToolInfo(ToolIds.Base64Converter, "Base64编码", "🔐", sp => sp.GetRequiredService<Base64ViewModel>()),
            new SubToolInfo(ToolIds.ColorConverter, "颜色转换", "🎨", sp => sp.GetRequiredService<ColorConverterViewModel>()),
            new SubToolInfo(ToolIds.PasswordGenerator, "密码生成", "🔑", sp => sp.GetRequiredService<PasswordGeneratorViewModel>()),
            new SubToolInfo(ToolIds.TextHasher, "文本哈希", "🔒", sp => sp.GetRequiredService<TextHasherViewModel>()),
            new SubToolInfo(ToolIds.TimestampConverter, "时间戳转换", "⏰", sp => sp.GetRequiredService<TimestampConverterViewModel>())
        ]);
    }
}
