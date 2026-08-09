using DS.Tools.Module.Base.Interfaces;

namespace DS.Tools.Module.Base.Services;

/// <summary>
/// 工具注册表实现 - 管理所有工具模块的注册和查询。
/// AOT 兼容，无运行时反射。当前选中工具状态由 <see cref="INavigationService"/> 持有。
/// 注册模块时顺带挂载统一目录到模块基类（模块元数据在 Register 阶段已入容器）。
/// </summary>
public sealed class ToolRegistry : IToolRegistry, IDisposable
{
    private readonly List<IToolModule> _tools = [];
    private readonly Dictionary<string, IToolModule> _toolIndex = new();
    private readonly IToolCatalog _toolCatalog;
    private bool _isDisposed;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="toolCatalog">统一工具目录（查询侧，容器构建后经集合注入）</param>
    public ToolRegistry(IToolCatalog toolCatalog)
    {
        _toolCatalog = toolCatalog ?? throw new ArgumentNullException(nameof(toolCatalog));
    }

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

        // 挂载统一目录：模块元数据在 Register 阶段已入容器（Build 前），
        // 此处（Build 后）将查询服务注入模块基类，SubTools/HasSubTools/CreateSubToolViewModel 即刻可用
        if (tool is ToolModule baseModule)
        {
            baseModule.AttachToolCatalog(_toolCatalog);
        }

        // 添加到列表和索引
        _tools.Add(tool);
        _toolIndex[tool.Id] = tool;
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _tools.Clear();
        _toolIndex.Clear();
        _isDisposed = true;
    }
}
