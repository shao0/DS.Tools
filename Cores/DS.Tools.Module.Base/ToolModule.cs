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
/// 子类须实现所有 abstract 成员。
/// </summary>
public abstract class ToolModule : IToolModule
{
    private SubToolManager? _subToolManager;

    /// <inheritdoc />
    public abstract string Id { get; }

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string Icon { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <summary>
    /// 子工具管理器（如果模块支持子工具）
    /// </summary>
    protected SubToolManager? SubToolManager => _subToolManager;

    /// <summary>
    /// 检查模块是否支持子工具
    /// </summary>
    public bool HasSubTools => _subToolManager?.Count > 0;

    /// <summary>
    /// 获取所有子工具（扩展友好API）
    /// </summary>
    public IReadOnlyList<SubToolInfo>? SubTools => _subToolManager?.SubTools;

    /// <summary>
    /// 初始化子工具管理器（在构造函数中调用）
    /// </summary>
    protected void EnableSubTools()
    {
        _subToolManager = new SubToolManager(Id);
    }

    /// <inheritdoc />
    /// <summary>
    /// 默认实现：从子工具管理器获取子工具的 ViewModel 工厂并经 DI 容器创建（IoC，AOT 兼容）。
    /// 子类可以重写此方法来自定义子工具解析逻辑。
    /// </summary>
    public virtual ViewModelBase? CreateSubToolViewModel(string subToolId, IServiceProvider services)
    {
        return _subToolManager?.GetSubToolViewModelFactory(subToolId)?.Invoke(services);
    }

    /// <inheritdoc />
    public abstract ViewModelBase CreateMainViewModel(IServiceProvider services);

    /// <inheritdoc />
    public abstract IServiceCollection Register(IServiceCollection services);

    /// <inheritdoc />
    public abstract void Initialize(IServiceProvider services);
}