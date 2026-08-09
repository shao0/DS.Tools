using DS.Tools.Core.Models;
using DS.Tools.Module.Base.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DS.Tools.Module.Base;

/// <summary>
/// 工具模块抽象基类 —— 实现 <see cref="IToolModule"/>（工具元数据契约）。
/// NativeAOT 兼容：模块构造函数不得依赖任何 DI 服务
/// （模块实例化发生在 BuildServiceProvider 之前；服务应通过 Register 注册，
/// 初始化逻辑放在 Initialize，所需服务从 IServiceProvider 解析）。
///
/// ViewModel 一律经 DI 容器（IoC）创建：模块提供强类型工厂
/// （如 <c>sp =&gt; sp.GetRequiredService&lt;JsonFormatterViewModel&gt;()</c>），
/// 杜绝 Type 键解析与运行时反射。
///
/// 子工具经 <see cref="ToolRegistration.AddSubTool{TViewModel, TView}"/> 在 Register 阶段注册进 DI 容器，
/// 容器构建后由 <see cref="ToolRegistry"/> 挂载 <see cref="IToolCatalog"/> 到模块基类——
/// <see cref="SubTools"/>/<see cref="CreateSubToolViewModel"/> 经统一目录查询，模块自身不再持有子工具状态。
///
/// 子类须实现所有 abstract 成员。
/// </summary>
public abstract class ToolModule : IToolModule
{
    private IToolCatalog? _toolCatalog;

    /// <inheritdoc />
    public abstract string Id { get; }

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string Icon { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <summary>
    /// 从子工具目录查询（挂载前返回 false，等价于无子工具）
    /// </summary>
    public bool HasSubTools => SubTools is { Count: > 0 };

    /// <summary>
    /// 从统一目录查询（挂载前返回 null）
    /// </summary>
    public IReadOnlyList<SubToolInfo>? SubTools => _toolCatalog?.GetSubTools(Id);

    /// <summary>
    /// 默认实现：从统一目录取得对应条目并经其 IoC 工厂创建（AOT 兼容）。
    /// 子类可以重写此方法来自定义子工具解析逻辑。
    /// </summary>
    public virtual ViewModelBase? CreateSubToolViewModel(string subToolId, IServiceProvider services)
        => _toolCatalog?.GetSubTool(Id, subToolId)?.CreateViewModel?.Invoke(services);

    /// <inheritdoc />
    public abstract ViewModelBase CreateMainViewModel(IServiceProvider services);

    /// <inheritdoc />
    public abstract IServiceCollection Register(IServiceCollection services);

    /// <inheritdoc />
    public abstract void Initialize(IServiceProvider services);

    /// <summary>
    /// 挂载统一目录（由 <see cref="ToolRegistry.Register"/> 在容器构建后调用）：
    /// 模块元数据在 Register 阶段已入容器，此处将查询服务引用注入模块基类，
    /// <see cref="SubTools"/>/<see cref="HasSubTools"/> 即刻可用。
    /// </summary>
    internal void AttachToolCatalog(IToolCatalog catalog)
    {
        _toolCatalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }
}
