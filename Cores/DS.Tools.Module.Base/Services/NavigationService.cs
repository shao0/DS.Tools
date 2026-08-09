using DS.Tools.Module.Base.Interfaces;

namespace DS.Tools.Module.Base.Services;

/// <summary>
/// 导航服务实现 - 管理工具模块间的导航
/// 支持模块级别和子工具级别的导航，AOT 兼容，无运行时反射
/// </summary>
public sealed class NavigationService : INavigationService
{
    private readonly IToolRegistry _toolRegistry;
    private readonly Stack<NavigationHistoryEntry> _navigationHistory = new();
    private IToolModule? _currentTool;
    private string? _currentSubToolId;

    /// <summary>
    /// 构造函数
    /// </summary>
    public NavigationService(IToolRegistry toolRegistry)
    {
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
    }

    /// <summary>
    /// 当前活动的工具模块
    /// </summary>
    public IToolModule? CurrentTool => _currentTool;

    /// <summary>
    /// 当前活动的子工具ID（如果有）
    /// </summary>
    public string? CurrentSubToolId => _currentSubToolId;

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
        // 检查是否为子工具导航（moduleId:subToolId 格式）
        if (toolId.Contains(':'))
        {
            var parts = toolId.Split(':');
            if (parts.Length == 2)
            {
                var moduleId = parts[0];
                var subToolId = parts[1];
                NavigateToSubTool(moduleId, subToolId);
                return;
            }
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

        // 保存当前导航状态到历史记录
        if (_currentTool is not null && (_currentTool != tool || _currentSubToolId is not null))
        {
            _navigationHistory.Push(new NavigationHistoryEntry(_currentTool, _currentSubToolId));
        }

        _currentTool = tool;
        _currentSubToolId = null; // 重置子工具ID

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

        // 保存当前导航状态到历史记录
        if (_currentTool is not null || _currentSubToolId is not null)
        {
            _navigationHistory.Push(new NavigationHistoryEntry(_currentTool, _currentSubToolId));
        }

        _currentTool = tool;
        _currentSubToolId = subToolId;

        // 触发导航变更事件
        NavigationChanged?.Invoke(tool, subToolId);
    }

    /// <summary>
    /// 导航回上一个工具
    /// </summary>
    public void NavigateBack()
    {
        if (_navigationHistory.Count == 0)
            return;

        var previousEntry = _navigationHistory.Pop();
        _currentTool = previousEntry.Tool;
        _currentSubToolId = previousEntry.SubToolId;

        // 触发导航变更事件
        NavigationChanged?.Invoke(previousEntry.Tool, previousEntry.SubToolId);
    }

    /// <summary>
    /// 导航历史记录条目
    /// </summary>
    private sealed record NavigationHistoryEntry(IToolModule? Tool, string? SubToolId);
}