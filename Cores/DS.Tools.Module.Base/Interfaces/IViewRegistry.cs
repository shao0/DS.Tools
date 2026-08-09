using Avalonia.Controls;

namespace DS.Tools.Module.Base.Interfaces;

/// <summary>
/// View 注册表（查询侧）- 渲染时按 ViewModel 实例解析对应 View（AOT 兼容，零反射）。
/// 映射声明在容器构建前由 <see cref="IViewMappingRegistry"/> 完成（模块 Register 内），
/// 本接口是 Build 后的只读查询入口：仅按 <c>viewModel.GetType()</c> 做字典键查询，
/// View 实例经 DI 容器 IoC 创建（GetRequiredService&lt;TView&gt;），无 Activator/Type.GetType 反射。
/// 替代主窗口 XAML 中手写的 DataTemplate 列表。
/// </summary>
public interface IViewRegistry
{
    /// <summary>
    /// 检查 ViewModel 实例的运行时类型是否已注册映射（仅字典键查询，非反射创建）
    /// </summary>
    /// <param name="viewModel">ViewModel 实例；null 返回 false</param>
    bool IsRegistered(object? viewModel);

    /// <summary>
    /// 为 ViewModel 实例创建对应 View（经 DI 容器 IoC）；未注册或入参为 null 时返回 null。
    /// 每次调用返回新实例（Transient，等价于 XAML DataTemplate 的实例化行为）。
    /// </summary>
    /// <param name="viewModel">ViewModel 实例；null 返回 null</param>
    Control? GetView(object? viewModel);
}
