using System.Collections.ObjectModel;
using DS.Tools.Core.Models;

namespace DS.Tools.Module.Base;

/// <summary>
/// 子工具信息 - 描述一个子工具的元数据。
/// AOT 兼容：创建 ViewModel 的工厂委托由模块在编译期提供（IoC，无 Type 键、无反射）。
/// </summary>
public sealed class SubToolInfo
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="id">子工具唯一标识符（在模块内唯一）</param>
    /// <param name="name">子工具显示名称</param>
    /// <param name="icon">子工具图标</param>
    /// <param name="createViewModel">ViewModel 工厂：经 DI 容器按强类型解析实例</param>
    public SubToolInfo(string id, string name, string icon, Func<IServiceProvider, ViewModelBase> createViewModel)
    {
        Id = id;
        Name = name;
        Icon = icon;
        CreateViewModel = createViewModel;
    }

    /// <summary>子工具唯一标识符（在模块内唯一）</summary>
    public string Id { get; }

    /// <summary>子工具显示名称</summary>
    public string Name { get; }

    /// <summary>子工具图标</summary>
    public string Icon { get; }

    /// <summary>ViewModel 工厂（IoC 创建，如 <c>sp => sp.GetRequiredService&lt;JsonFormatterViewModel&gt;()</c>）</summary>
    public Func<IServiceProvider, ViewModelBase> CreateViewModel { get; }

    /// <summary>完整导航ID（格式：moduleId:subToolId）</summary>
    public string GetFullNavigationId(string moduleId) => $"{moduleId}:{Id}";
}

/// <summary>
/// 子工具管理器 - 提供扩展友好的子工具管理API
/// AOT 兼容，支持运行时动态添加子工具
/// </summary>
public sealed class SubToolManager
{
    private readonly Dictionary<string, SubToolInfo> _subTools = new();
    private readonly string _moduleId;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="moduleId">所属模块的ID</param>
    public SubToolManager(string moduleId)
    {
        _moduleId = moduleId ?? throw new ArgumentNullException(nameof(moduleId));
    }

    /// <summary>
    /// 获取所有子工具
    /// </summary>
    public IReadOnlyList<SubToolInfo> SubTools => _subTools.Values.ToList();

    /// <summary>
    /// 添加子工具（AOT 兼容）
    /// </summary>
    public void AddSubTool(SubToolInfo subTool)
    {
        ArgumentNullException.ThrowIfNull(subTool);

        if (_subTools.ContainsKey(subTool.Id))
        {
            throw new InvalidOperationException($"SubTool with ID '{subTool.Id}' already exists in module '{_moduleId}'.");
        }

        _subTools[subTool.Id] = subTool;
    }

    /// <summary>
    /// 批量添加子工具
    /// </summary>
    public void AddSubTools(IEnumerable<SubToolInfo> subTools)
    {
        foreach (var subTool in subTools)
        {
            AddSubTool(subTool);
        }
    }

    /// <summary>
    /// 根据ID获取子工具
    /// </summary>
    public SubToolInfo? GetSubTool(string subToolId)
    {
        return _subTools.GetValueOrDefault(subToolId);
    }

    /// <summary>
    /// 获取子工具的 ViewModel 工厂（IoC 创建，AOT 兼容）
    /// </summary>
    public Func<IServiceProvider, ViewModelBase>? GetSubToolViewModelFactory(string subToolId)
    {
        return GetSubTool(subToolId)?.CreateViewModel;
    }

    /// <summary>
    /// 检查是否包含指定子工具
    /// </summary>
    public bool ContainsSubTool(string subToolId)
    {
        return _subTools.ContainsKey(subToolId);
    }

    /// <summary>
    /// 获取子工具数量
    /// </summary>
    public int Count => _subTools.Count;

    /// <summary>
    /// 清空所有子工具
    /// </summary>
    public void Clear()
    {
        _subTools.Clear();
    }
}
