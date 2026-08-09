namespace DS.Tools.Module.Base.Interfaces;

/// <summary>
/// 工具注册表接口 - 管理所有工具模块的注册和查询。
/// 当前选中工具状态由 INavigationService 持有，注册表不维护重复状态。
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// 获取所有已注册工具
    /// </summary>
    IReadOnlyList<IToolModule> Tools { get; }

    /// <summary>
    /// 根据 ID 获取工具
    /// </summary>
    /// <param name="id">工具唯一标识符</param>
    /// <returns>工具模块实例，未找到时返回 null</returns>
    IToolModule? GetTool(string id);

    /// <summary>
    /// 注册工具（编译期显式调用，非运行时反射）
    /// </summary>
    /// <param name="tool">工具模块实例</param>
    void Register(IToolModule tool);
}