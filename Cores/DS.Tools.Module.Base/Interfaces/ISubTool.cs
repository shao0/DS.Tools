namespace DS.Tools.Module.Base.Interfaces;

/// <summary>
/// 子工具 ViewModel 契约 - ViewModel 实现此接口声明自身元数据（静态抽象接口成员，C# 14）。
/// 由 <see cref="DS.Tools.Module.Base.Services.ToolRegistration.AddSubTool{TViewModel}"/> 编译期读取
/// （<c>TViewModel.ModuleId</c> 经 constrained call，无需实例化、无 Type 键、零反射，AOT 安全），
/// 注册为 SubToolInfo 供 <see cref="IToolCatalog"/> 查询。
/// 实现类用显式接口实现（<c>static string ISubTool.Id =&gt; ...</c>），不污染 ViewModel 公共 API。
/// </summary>
public interface ISubTool
{
    /// <summary>所属模块的 ID（导航 ID 前缀）</summary>
    static abstract string ModuleId { get; }

    /// <summary>子工具唯一标识符（在模块内唯一）</summary>
    static abstract string Id { get; }

    /// <summary>子工具显示名称</summary>
    static abstract string Name { get; }

    /// <summary>子工具图标</summary>
    static abstract string Icon { get; }
}
