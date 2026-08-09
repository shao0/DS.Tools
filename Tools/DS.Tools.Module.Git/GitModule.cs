using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DS.Tools.Module.Base;
using DS.Tools.Module.Base.Services;
using DS.Tools.Module.Git.Services;
using DS.Tools.Module.Git.ViewModels;
using DS.Tools.Module.Git.Views;

namespace DS.Tools.Module.Git;

/// <summary>
/// Git 工具模块 - 浏览指定 Git 仓库的提交历史。
/// 子工具经 AddSubTool 在 Register 阶段注册进 DI 容器（SubToolInfo 含 IoC 工厂，无 Type 键、无反射），
/// 由 IToolCatalog 统一目录按模块查询并经 ToolRegistry 挂载到基类。
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

    public override IServiceCollection Register(IServiceCollection services)
    {
        // 注册子工具：AddSubTool 一行完成「ViewModel 入容器 + SubToolInfo（含 IoC 工厂）入容器」——
        // 元数据由 ViewModel 实现的 ISubTool 静态抽象接口提供（编译期读取），无 Type 键、零反射，AOT 兼容；
        // Build 后经 IToolCatalog 统一目录按模块查询
        services.AddSubTool<GitLogViewModel>();

        // 注册 View 映射：AddViewMapping 一行完成「VM + View 入容器 + ViewModel→View 映射」——
        // IoC 经 DI 容器创建，类型模式匹配无 Type 键，AOT 兼容零反射，替代 XAML DataTemplate 手写列表
        services.AddViewMapping<GitLogViewModel, GitLogView>();

        // 注册共享服务（单例：git 服务无状态、设置服务文件级持久化）
        services.AddSingleton<IGitLogService, GitLogService>();
        services.AddSingleton<IGitSettingsService, GitSettingsService>();

        return services;
    }

    public override void Initialize(IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILogger<GitModule>>();
        logger.LogInformation("模块 {ModuleId} 初始化完成，子工具 {SubToolCount} 个", Id, SubTools?.Count ?? 0);
    }
}
