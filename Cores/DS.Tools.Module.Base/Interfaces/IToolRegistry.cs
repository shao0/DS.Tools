namespace DS.Tools.Module.Base.Interfaces;

/// <summary>
/// 工具注册表接口 - 管理所有工具模块的注册和查询
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

    /// <summary>
    /// 当前选中的工具
    /// </summary>
    IToolModule? ActiveTool { get; set; }

    /// <summary>
    /// 工具变更事件（当 ActiveTool 改变时触发，标准 .NET 事件）
    /// </summary>
    event Action<IToolModule?>? ToolChanged;
}