using Microsoft.Extensions.DependencyInjection;

namespace DS.Tools.Module.Base.Interfaces;

/// <summary>
/// 工具模块契约 - 定义工具模块的元数据和生命周期
/// AOT 兼容，编译期显式注册，无运行时反射
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
    /// 主 ViewModel 类型（用于导航和 View 定位）
    /// </summary>
    Type ViewModelType { get; }

    /// <summary>
    /// 检查模块是否支持子工具
    /// </summary>
    bool HasSubTools { get; }

    /// <summary>
    /// 获取所有子工具（如果模块支持子工具）
    /// </summary>
    IReadOnlyList<SubToolInfo>? SubTools { get; }

    /// <summary>
    /// 获取子工具的 ViewModel 类型（AOT 兼容）
    /// 如果子工具不存在，返回 null
    /// </summary>
    /// <param name="subToolId">子工具ID</param>
    /// <returns>子工具的ViewModel类型，不存在则返回null</returns>
    Type? GetSubToolViewModelType(string subToolId);

    /// <summary>
    /// 注册模块服务和 ViewModel 到 DI 容器
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
