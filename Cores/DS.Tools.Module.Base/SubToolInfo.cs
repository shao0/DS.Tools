using DS.Tools.Core.Models;

namespace DS.Tools.Module.Base;

/// <summary>
/// 子工具信息 - 描述一个子工具的元数据。
/// AOT 兼容：创建 ViewModel 的工厂委托由模块在编译期提供（IoC，无 Type 键、无反射）。
/// 经 <see cref="DS.Tools.Module.Base.Services.ToolRegistration.AddSubTool{TViewModel}"/> 扩展方法以单例注册进 DI 容器，
/// 容器构建后由 <see cref="DS.Tools.Module.Base.Interfaces.IToolCatalog"/> 按模块查询。
/// </summary>
public sealed class SubToolInfo
{
    /// <summary>
    /// 构造函数
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
    }

    /// <summary>所属模块的 ID</summary>
    public string ModuleId { get; }

    /// <summary>子工具唯一标识符（在模块内唯一）</summary>
    public string Id { get; }

    /// <summary>子工具显示名称</summary>
    public string Name { get; }

    /// <summary>子工具图标</summary>
    public string Icon { get; }

    /// <summary>ViewModel 工厂（IoC 创建，如 <c>sp => sp.GetRequiredService&lt;JsonFormatterViewModel&gt;()</c>）</summary>
    public Func<IServiceProvider, ViewModelBase> CreateViewModel { get; }

    /// <summary>完整导航ID（格式：moduleId:subToolId，使用自身的 ModuleId）</summary>
    public string GetFullNavigationId() => $"{ModuleId}:{Id}";
}
