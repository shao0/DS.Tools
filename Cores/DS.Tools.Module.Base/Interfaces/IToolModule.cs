using DS.Tools.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DS.Tools.Module.Base.Interfaces;

/// <summary>
/// 工具模块契约 - 定义工具模块的元数据和生命周期。
/// AOT 兼容：编译期显式注册，无运行时反射，无 Type 键创建——
/// ViewModel 一律由 DI 容器（IoC）通过强类型工厂创建。
/// </summary>
public interface IToolModule
{
    /// <summary>
    /// 模块唯一标识符
    /// </summary>
    string Id { get; }

    /// <summary>
    /// 模块显示名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 模块图标（emoji 或图标资源键）
    /// </summary>
    string Icon { get; }

    /// <summary>
    /// 模块描述
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 检查模块是否支持子工具
    /// </summary>
    bool HasSubTools { get; }

    /// <summary>
    /// 获取所有子工具（如果模块支持子工具）
    /// </summary>
    IReadOnlyList<SubToolInfo>? SubTools { get; }

    /// <summary>
    /// 通过 DI 容器创建模块主 ViewModel（IoC 创建，编译期泛型，无 Type 键）。
    /// </summary>
    /// <param name="services">服务提供者</param>
    /// <returns>主 ViewModel 实例</returns>
    ViewModelBase CreateMainViewModel(IServiceProvider services);

    /// <summary>
    /// 通过 DI 容器创建子工具的 ViewModel；子工具不存在时返回 null。
    /// </summary>
    /// <param name="subToolId">子工具ID</param>
    /// <param name="services">服务提供者</param>
    /// <returns>子工具 ViewModel 实例，不存在则返回 null</returns>
    ViewModelBase? CreateSubToolViewModel(string subToolId, IServiceProvider services);

    /// <summary>
    /// 注册模块的全部依赖：子工具经 <c>AddSubTool&lt;TViewModel, TView&gt;</c> 扩展方法一行完成
    /// （VM + View 入容器 + SubToolInfo 元数据/工厂/View 映射入容器，编译期强类型，零反射）。
    /// 在容器构建前调用。
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>更新后的服务集合（链式调用）</returns>
    IServiceCollection Register(IServiceCollection services);

    /// <summary>
    /// 初始化模块（在容器构建后调用）
    /// </summary>
    /// <param name="services">服务提供者</param>
    void Initialize(IServiceProvider services);
}
