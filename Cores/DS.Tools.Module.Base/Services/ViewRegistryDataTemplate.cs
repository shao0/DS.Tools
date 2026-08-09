using Avalonia.Controls;
using Avalonia.Controls.Templates;
using DS.Tools.Module.Base.Interfaces;

namespace DS.Tools.Module.Base.Services;

/// <summary>
/// 桥接 Avalonia 模板机制与 <see cref="IToolCatalog"/> 的 DataTemplate——
/// 挂到主窗口 DataTemplates 集合后，ContentControl 的内容（ViewModel）
/// 经目录解析为对应 View，替代逐条手写 DataTemplate。
/// AOT 兼容：注册表查询为编译期类型模式匹配，View 实例化经 IoC 工厂（编译期泛型），零反射。
/// </summary>
/// <param name="catalog">工具目录（经 DI 注入，统一注册表查询服务）</param>
public sealed class ViewRegistryDataTemplate(IToolCatalog catalog) : IDataTemplate
{
    /// <inheritdoc />
    public Control? Build(object? data) => catalog.GetView(data);

    /// <inheritdoc />
    public bool Match(object? data) => catalog.IsRegistered(data);
}
