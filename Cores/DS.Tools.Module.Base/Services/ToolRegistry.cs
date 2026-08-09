using DS.Tools.Module.Base.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DS.Tools.Module.Base.Services;

/// <summary>
/// 工具注册表实现 - 管理所有工具模块的注册和查询
/// AOT 兼容，无运行时反射，使用标准 .NET 事件
/// </summary>
public sealed class ToolRegistry : IToolRegistry, IDisposable
{
    private readonly List<IToolModule> _tools = [];
    private readonly Dictionary<string, IToolModule> _toolIndex = new();
    private IToolModule? _activeTool;
    private bool _isDisposed;

    /// <summary>
    /// 获取所有已注册工具
    /// </summary>
    public IReadOnlyList<IToolModule> Tools => _tools;

    /// <summary>
    /// 根据 ID 获取工具
    /// </summary>
    public IToolModule? GetTool(string id)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, typeof(ToolRegistry));

        return _toolIndex.GetValueOrDefault(id);
    }

    /// <summary>
    /// 注册工具（编译期显式调用）
    /// </summary>
    public void Register(IToolModule tool)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, typeof(ToolRegistry));

        ArgumentNullException.ThrowIfNull(tool);

        // 检查重复
        if (_toolIndex.ContainsKey(tool.Id))
        {
            throw new InvalidOperationException($"Tool '{tool.Id}' is already registered.");
        }

        // 添加到列表和索引
        _tools.Add(tool);
        _toolIndex[tool.Id] = tool;
    }

    /// <summary>
    /// 当前选中的工具
    /// </summary>
    public IToolModule? ActiveTool
    {
        get => _activeTool;
        set
        {
            ObjectDisposedException.ThrowIf(_isDisposed, typeof(ToolRegistry));

            if (_activeTool != value)
            {
                _activeTool = value;
                ToolChanged?.Invoke(value);
            }
        }
    }

    /// <summary>
    /// 工具变更事件（标准 .NET 事件）
    /// </summary>
    public event Action<IToolModule?>? ToolChanged;

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _tools.Clear();
        _toolIndex.Clear();
        _activeTool = null;
        _isDisposed = true;
    }
}