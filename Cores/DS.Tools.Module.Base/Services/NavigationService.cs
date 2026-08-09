using DS.Tools.Module.Base.Interfaces;

namespace DS.Tools.Module.Base.Services;

/// <summary>
/// 导航服务实现 - 管理工具模块间的导航
/// 支持模块级别和子工具级别的导航，AOT 兼容，无运行时反射
/// </summary>
internal sealed class NavigationService : INavigationService
{
    private readonly IToolRegistry _toolRegistry;

    /// <summary>
    /// 构造函数
    /// </summary>
    public NavigationService(IToolRegistry toolRegistry)
    {
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
    }

    /// <summary>
    /// 导航变更事件（参数：当前工具模块，当前子工具ID）
    /// </summary>
    public event Action<IToolModule?, string?>? NavigationChanged;

    /// <summary>
    /// 导航到指定工具模块或子工具
    /// 支持：toolId 或 moduleId:subToolId 格式
    /// </summary>
    public void NavigateTo(string toolId)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(toolId);

        // 子工具导航（moduleId:subToolId 格式）：取第一个冒号为分隔符
        var separatorIndex = toolId.IndexOf(':');
        if (separatorIndex > 0)
        {
            NavigateToSubTool(toolId[..separatorIndex], toolId[(separatorIndex + 1)..]);
            return;
        }

        // 常规模块导航
        var tool = _toolRegistry.GetTool(toolId);
        if (tool is null)
        {
            throw new InvalidOperationException($"Tool with ID '{toolId}' not found.");
        }

        NavigateTo(tool);
    }

    /// <summary>
    /// 导航到指定工具模块
    /// </summary>
    public void NavigateTo(IToolModule tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        // 触发导航变更事件
        NavigationChanged?.Invoke(tool, null);
    }

    /// <summary>
    /// 导航到指定子工具
    /// </summary>
    private void NavigateToSubTool(string moduleId, string subToolId)
    {
        var tool = _toolRegistry.GetTool(moduleId);
        if (tool is null)
        {
            throw new InvalidOperationException($"Tool module with ID '{moduleId}' not found.");
        }

        // 触发导航变更事件
        NavigationChanged?.Invoke(tool, subToolId);
    }
}
