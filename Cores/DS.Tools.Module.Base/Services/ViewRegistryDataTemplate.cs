using Avalonia.Controls;
using Avalonia.Controls.Templates;
using DS.Tools.Module.Base.Interfaces;

namespace DS.Tools.Module.Base.Services;

/// <summary>
/// 桥接 Avalonia 模板机制与 <see cref="IViewRegistry"/> 的 DataTemplate——
/// 挂到主窗口 DataTemplates 集合后，ContentControl 的内容（ViewModel）
/// 经注册表解析为对应 View，替代逐条手写 DataTemplate。
/// AOT 兼容：注册表查询是字典操作，View 实例化是编译期 <c>new TView()</c>，零反射。
/// </summary>
/// <param name="viewRegistry">View 注册表（经 DI 注入）</param>
public sealed class ViewRegistryDataTemplate(IViewRegistry viewRegistry) : IDataTemplate
{
    /// <inheritdoc />
    public Control? Build(object? data) => viewRegistry.GetView(data);

    /// <inheritdoc />
    public bool Match(object? data) => viewRegistry.IsRegistered(data);
}
