using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DS.Tools.Module.Base;
using DS.Tools.Module.Git.Services;
using DS.Tools.Module.Git.ViewModels;

namespace DS.Tools.Module.Git;

/// <summary>
/// Git 工具模块 - 浏览指定 Git 仓库的提交历史。
/// 扩展友好：使用 SubToolManager 管理子工具，ViewModel 经 DI 容器按强类型解析（IoC，无 Type 键、无反射）。
/// </summary>
public sealed class GitModule : ToolModule
{
    /// <summary>
    /// Git 模块及子工具的导航 ID 常量（全模块唯一引用点，避免魔法字符串散落）
    /// </summary>
    public static class ToolIds
    {
        /// <summary>模块 ID</summary>
        public const string Module = "git-tools";

        /// <summary>子工具：Git 日志</summary>
        public const string Log = "git-log";

        /// <summary>完整导航 ID（module:subTool）</summary>
        public static string Full(string subToolId) => $"{Module}:{subToolId}";
    }

    public override string Id => ToolIds.Module;
    public override string Name => "Git 工具";
    public override string Icon => "🐙";
    public override string Description => "Git 仓库工具，包含提交日志浏览等常用操作";

    /// <inheritdoc />
    /// <summary>
    /// 主 ViewModel 工厂：经 DI 容器按强类型解析（IoC，无 Type 键、无反射）
    /// </summary>
    public override ViewModelBase CreateMainViewModel(IServiceProvider services)
        => services.GetRequiredService<GitLogViewModel>();

    /// <summary>
    /// 构造函数 - 启用子工具支持
    /// </summary>
    public GitModule()
    {
        EnableSubTools();
        InitializeSubTools();
    }

    public override IServiceCollection Register(IServiceCollection services)
    {
        // 注册所有 ViewModel
        services.AddTransient<GitLogViewModel>();

        // 注册共享服务（单例：git 服务无状态、设置服务文件级持久化）
        services.AddSingleton<IGitLogService, GitLogService>();
        services.AddSingleton<IGitSettingsService, GitSettingsService>();

        // 注意：ViewModel→View 映射不在此注册——
        // 由主应用 MainWindow.axaml 中的编译期 DataTemplate 声明（AOT 兼容，零反射）。

        return services;
    }

    public override void Initialize(IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILogger<GitModule>>();
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
            new SubToolInfo(ToolIds.Log, "Git 日志", "📜", sp => sp.GetRequiredService<GitLogViewModel>())
        ]);
    }
}
