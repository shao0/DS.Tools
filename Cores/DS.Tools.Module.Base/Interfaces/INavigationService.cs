namespace DS.Tools.Module.Base.Interfaces;

/// <summary>
/// 导航服务接口 - 管理工具模块间的导航
/// 支持模块级别和子工具级别的导航
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// 导航到指定工具模块或子工具
    /// 支持：toolId 或 moduleId:subToolId 格式
    /// </summary>
    /// <param name="toolId">工具模块ID或子工具完整ID</param>
    void NavigateTo(string toolId);

    /// <summary>
    /// 导航到指定工具模块
    /// </summary>
    /// <param name="tool">工具模块</param>
    void NavigateTo(IToolModule tool);

    /// <summary>
    /// 导航变更事件（参数：当前工具模块，当前子工具ID）
    /// </summary>
    event Action<IToolModule?, string?>? NavigationChanged;
}
