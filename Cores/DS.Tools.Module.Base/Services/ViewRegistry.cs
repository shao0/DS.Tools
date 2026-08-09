using Avalonia.Controls;
using DS.Tools.Module.Base.Interfaces;

namespace DS.Tools.Module.Base.Services;

/// <summary>
/// View 注册表实现（查询侧，Build 后）- 消费容器内注册的 <see cref="ViewMappingEntry"/> 集合。
/// 匹配经条目委托（is TViewModel 类型模式）逐个判定，**无 Type 键**；
/// View 实例经 DI 容器 IoC 创建（Transient，与 ViewModel 同纪律）。
/// AOT 兼容：类型模式与泛型工厂均为编译期静态引用，无反射创建路径。
/// </summary>
/// <param name="mappings">容器内注册的 View 映射条目（MEL 集合注入，按注册顺序）</param>
/// <param name="services">DI 容器（MEL 支持注入 IServiceProvider 本身）</param>
public sealed class ViewRegistry : IViewRegistry
{
    private readonly IReadOnlyList<ViewMappingEntry> _mappings;
    private readonly IServiceProvider _services;

    /// <summary>
    /// 构造函数（经 DI 解析映射条目集合 + 容器）
    /// </summary>
    public ViewRegistry(IEnumerable<ViewMappingEntry> mappings, IServiceProvider services)
    {
        _services = services;

        // 反转注册顺序：后注册的映射优先匹配（覆盖语义）
        _mappings = mappings.Reverse().ToArray();
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
}
