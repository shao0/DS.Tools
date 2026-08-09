using Avalonia.Controls;
using DS.Tools.Module.Base.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DS.Tools.Module.Base.Services;

/// <summary>
/// 工具目录实现（查询侧，统一注册表查询服务）-
/// 合并 View 映射查询与子工具查询，条目统一为 <see cref="SubToolInfo"/>（单集合注入）：
/// 构造时经 MEL 集合注入收集 Register 阶段入容器的全部条目，
/// View 映射取含映射的条目并反转（后注册者优先，覆盖语义）、子工具取含元数据的条目按 ModuleId 建立查找索引。
/// AOT 兼容：View 匹配为编译期类型模式（is 委托），子工具为 string 键分组，无 Type 键、无运行时反射；
/// View 实例经 IoC 工厂创建，派生类 VM 天然命中基类映射。
/// </summary>
internal sealed class ToolCatalog : IToolCatalog
{
    private readonly IReadOnlyList<SubToolInfo> _mappings;
    private readonly ILookup<string, SubToolInfo> _byModule;
    private readonly IServiceProvider _services;

    /// <summary>
    /// 构造函数（MEL 集合注入：按注册顺序收集全部条目）
    /// </summary>
    /// <param name="entries">注册条目集合（子工具与仅 View 映射条目同源）</param>
    /// <param name="services">服务提供者（View 经 IoC 工厂创建）</param>
    public ToolCatalog(IEnumerable<SubToolInfo> entries, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(entries);

        _services = services ?? throw new ArgumentNullException(nameof(services));
        _mappings = entries.Where(e => e.MatchView is not null).Reverse().ToArray(); // 后注册者优先匹配（覆盖语义）
        _byModule = entries.Where(e => e.ModuleId is not null).ToLookup(e => e.ModuleId!);
    }

    /// <inheritdoc />
    public bool IsRegistered(object? viewModel)
        => viewModel is not null && _mappings.Any(e => e.MatchView!(viewModel));

    /// <inheritdoc />
    public Control? GetView(object? viewModel)
        => viewModel is not null
           && _mappings.FirstOrDefault(e => e.MatchView!(viewModel)) is { } entry
            ? entry.BuildView!(_services)
            : null;

    /// <inheritdoc />
    public IReadOnlyList<SubToolInfo> GetSubTools(string moduleId)
        => _byModule[moduleId].ToList();

    /// <inheritdoc />
    public SubToolInfo? GetSubTool(string moduleId, string subToolId)
        => _byModule[moduleId].FirstOrDefault(s => s.Id == subToolId);
}
