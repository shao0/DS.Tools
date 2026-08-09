using Avalonia.Controls;
using DS.Tools.Core.Models;

namespace DS.Tools.Module.Base;

/// <summary>
/// 子工具注册条目 - 统一承载子工具元数据（导航/侧边栏/磁贴）与 ViewModel→View 映射（渲染）。
/// 合并自 ViewMappingEntry：注册侧一个条目即完成「VM + View 入容器」+「元数据 + 工厂 + 映射」，
/// 目录侧仅注入 <c>IEnumerable&lt;SubToolInfo&gt;</c> 单集合即可同时查询子工具与 View。
/// AOT 兼容：元数据经 <see cref="Interfaces.ISubTool"/> 静态抽象接口编译期读取，
/// 创建/构建均为编译期泛型工厂（<c>sp.GetRequiredService&lt;T&gt;()</c>），无 Type 键、无反射。
/// </summary>
public sealed class SubToolInfo
{
    /// <summary>
    /// 构造函数（子工具条目：含元数据 + View 映射；导航测试可直接构造，映射缺省即不参与 View 渲染）
    /// </summary>
    /// <param name="moduleId">所属模块的 ID（导航 ID 前缀）</param>
    /// <param name="id">子工具唯一标识符（在模块内唯一）</param>
    /// <param name="name">子工具显示名称</param>
    /// <param name="icon">子工具图标</param>
    /// <param name="createViewModel">ViewModel 工厂：经 DI 容器按强类型解析实例</param>
    public SubToolInfo(string moduleId, string id, string name, string icon, Func<IServiceProvider, ViewModelBase> createViewModel)
    {
        ModuleId = moduleId;
        Id = id;
        Name = name;
        Icon = icon;
        CreateViewModel = createViewModel;
        MatchView = null;
        BuildView = null;
    }

    /// <summary>
    /// 构造函数（完整条目：子工具元数据 + View 映射；仅 <see cref="Services.ToolRegistration"/> 使用）
    /// </summary>
    internal SubToolInfo(
        string moduleId,
        string id,
        string name,
        string icon,
        Func<IServiceProvider, ViewModelBase> createViewModel,
        Func<object?, bool>? matchView,
        Func<IServiceProvider, Control>? buildView)
    {
        ModuleId = moduleId;
        Id = id;
        Name = name;
        Icon = icon;
        CreateViewModel = createViewModel;
        MatchView = matchView;
        BuildView = buildView;
    }

    /// <summary>
    /// 构造函数（仅 View 映射条目：应用级页面如主页，不属于任何模块；仅 <see cref="Services.ToolRegistration"/> 使用）
    /// </summary>
    internal SubToolInfo(Func<object?, bool> matchView, Func<IServiceProvider, Control> buildView)
    {
        MatchView = matchView;
        BuildView = buildView;
    }

    /// <summary>所属模块的 ID（仅 View 映射条目为 null）</summary>
    public string? ModuleId { get; }

    /// <summary>子工具唯一标识符（在模块内唯一；仅 View 映射条目为 null）</summary>
    public string? Id { get; }

    /// <summary>子工具显示名称（仅 View 映射条目为 null）</summary>
    public string? Name { get; }

    /// <summary>子工具图标（仅 View 映射条目为 null）</summary>
    public string? Icon { get; }

    /// <summary>ViewModel 工厂（IoC 创建，如 <c>sp =&gt; sp.GetRequiredService&lt;JsonFormatterViewModel&gt;()</c>；仅 View 映射条目为 null）</summary>
    public Func<IServiceProvider, ViewModelBase>? CreateViewModel { get; }

    /// <summary>View 匹配委托（编译期类型模式：<c>vm is TViewModel</c>；无映射条目为 null）</summary>
    internal Func<object?, bool>? MatchView { get; }

    /// <summary>View 构建委托（IoC 工厂：<c>sp.GetRequiredService&lt;TView&gt;()</c>）</summary>
    internal Func<IServiceProvider, Control>? BuildView { get; }

    /// <summary>完整导航ID（格式：moduleId:subToolId，使用自身的 ModuleId）</summary>
    public string GetFullNavigationId() => $"{ModuleId}:{Id}";
}
