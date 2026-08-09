using Avalonia.Controls;
using DS.Tools.Module.Base.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DS.Tools.Module.Base.Services;

/// <summary>
/// 工具目录实现（查询侧，统一注册表查询服务）-
/// 合并 View 映射查询（原 ViewRegistry）与子工具查询（原 SubToolCatalog）：
/// 构造时经 MEL 集合注入收集 Register 阶段入容器的全部 <see cref="ViewMappingEntry"/> 与 <see cref="SubToolInfo"/>，
/// View 映射反转集合（后注册者优先匹配，覆盖语义）、子工具按 ModuleId 建立查找索引。
/// AOT 兼容：View 匹配为编译期类型模式（is 委托），子工具为 string 键分组，无 Type 键、无运行时反射；
/// View 实例经 IoC 工厂创建，派生类 VM 天然命中基类映射。
/// </summary>
public sealed class ToolCatalog : IToolCatalog
{
    private readonly IReadOnlyList<ViewMappingEntry> _mappings;
    private readonly ILookup<string, SubToolInfo> _byModule;
    private readonly IServiceProvider _services;

    /// <summary>
    /// 构造函数（MEL 集合注入：按注册顺序收集全部映射与子工具）
    /// </summary>
    /// <param name="mappings">View 映射条目集合</param>
    /// <param name="subTools">子工具集合</param>
    /// <param name="services">服务提供者（View 经 IoC 工厂创建）</param>
    public ToolCatalog(IEnumerable<ViewMappingEntry> mappings, IEnumerable<SubToolInfo> subTools, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(mappings);
        ArgumentNullException.ThrowIfNull(subTools);

        _services = services ?? throw new ArgumentNullException(nameof(services));
        _mappings = mappings.Reverse().ToArray(); // 后注册者优先匹配（覆盖语义）
        _byModule = subTools.ToLookup(s => s.ModuleId);
    }

    /// <inheritdoc />
    public bool IsRegistered(object? viewModel)
        => viewModel is not null && _mappings.Any(e => e.Match(viewModel));

    /// <inheritdoc />
    public Control? GetView(object? viewModel)
        => viewModel is not null
           && _mappings.FirstOrDefault(e => e.Match(viewModel)) is { } entry
            ? entry.Build(viewModel, _services)
            : null;

    /// <inheritdoc />
    public IReadOnlyList<SubToolInfo> GetSubTools(string moduleId)
        => _byModule[moduleId].ToList();

    /// <inheritdoc />
    public SubToolInfo? GetSubTool(string moduleId, string subToolId)
        => _byModule[moduleId].FirstOrDefault(s => s.Id == subToolId);
}

/// <summary>
/// View 映射条目（注册进 DI 容器的数据项；经 MEL 集合注入由 <see cref="ToolCatalog"/> 消费）。
/// 实例在 Build 前创建，成员仅供 ToolCatalog（同程序集）查询。
/// 无 Type 键——持 Match 委托（类型模式）与 Build 委托（IoC 工厂），AOT 兼容零反射。
/// </summary>
public sealed class ViewMappingEntry
{
    /// <summary>
    /// 创建映射条目（仅 <see cref="ToolRegistration"/> 内部使用）
    /// </summary>
    internal ViewMappingEntry(Func<object?, bool> match, Func<object?, IServiceProvider, Control> build)
    {
        Match = match;
        Build = build;
    }

    /// <summary>匹配委托（编译期类型模式：<c>vm is TViewModel</c>）</summary>
    internal Func<object?, bool> Match { get; }

    /// <summary>构建委托（IoC 工厂：<c>sp.GetRequiredService&lt;TView&gt;()</c>）</summary>
    internal Func<object?, IServiceProvider, Control> Build { get; }
}
