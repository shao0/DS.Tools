using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace DS.Tools.Module.Base;

/// <summary>
/// 子工具信息 - 描述一个子工具的元数据
/// AOT 兼容，编译期类型安全
/// </summary>
public sealed class SubToolInfo
{
    public SubToolInfo(string id, string name, string icon, Type viewModelType)
    {
        Id = id;
        Name = name;
        Icon = icon;
        ViewModelType = viewModelType;
    }

    /// <summary>子工具唯一标识符（在模块内唯一）</summary>
    public string Id { get; }

    /// <summary>子工具显示名称</summary>
    public string Name { get; }

    /// <summary>子工具图标</summary>
    public string Icon { get; }

    /// <summary>对应的 ViewModel 类型</summary>
    public Type ViewModelType { get; }

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SubToolInfo? GetSubTool(string subToolId)
    {
        return _subTools.GetValueOrDefault(subToolId);
    }

    /// <summary>
    /// 获取子工具的 ViewModel 类型（AOT 兼容）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Type? GetSubToolViewModelType(string subToolId)
    {
        var subTool = GetSubTool(subToolId);
        return subTool?.ViewModelType;
    }

    /// <summary>
    /// 检查是否包含指定子工具
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        _subTools.Clear();
    }
}
