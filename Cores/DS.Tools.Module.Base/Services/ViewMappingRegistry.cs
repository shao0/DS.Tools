using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace DS.Tools.Module.Base.Services;

/// <summary>
/// View 映射注册表（静态封装 + IServiceCollection 扩展方法）- 容器构建前声明
/// ViewModel → View 映射（AOT 兼容，零反射）。
/// 每个映射以 <see cref="ViewMappingEntry"/> 实例注册进 DI 容器（Build 前，纯数据），
/// Build 后由 <see cref="ViewRegistry"/> 经 MEL 集合注入（IEnumerable&lt;ViewMappingEntry&gt;）消费。
/// </summary>
public static class ViewMappingRegistry
{
    /// <summary>
    /// 注册 ViewModel 与 View（均以 Transient 入 DI 容器，IoC 创建）+ 声明 ViewModel → View 映射
    /// （一行完成 View 接线，替代 XAML DataTemplate 手写列表）。
    /// 匹配经类型模式（is TViewModel）判定——无 Type 键，天然支持派生类 VM 命中基类映射；
    /// 同一 ViewModel 重复注册时后注册者优先（覆盖语义，ViewRegistry 反转集合实现）。
    /// </summary>
    /// <typeparam name="TViewModel">ViewModel 类型（注册进容器 + 类型模式匹配）</typeparam>
    /// <typeparam name="TView">View 类型（注册进容器，经 DI 解析）</typeparam>
    public static IServiceCollection AddViewMapping<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TViewModel, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TView>(this IServiceCollection services)
        where TViewModel : class
        where TView : Control
    {
        // View 与 ViewModel 同时注册进容器（Transient，与 ViewModel 同纪律，支持构造依赖注入）
        services.AddTransient<TViewModel>();
        services.AddTransient<TView>();

        // 映射条目：类型模式匹配（编译期静态类型检查）+ IoC 工厂（编译期 new 实例化路径）
        services.AddSingleton(new ViewMappingEntry(
            vm => vm is TViewModel,
            (_, sp) => sp.GetRequiredService<TView>()));
        return services;
    }
}

/// <summary>
/// View 映射条目（注册进 DI 容器的数据项；经 MEL 集合注入由 ViewRegistry 消费）。
/// 以委托表达匹配与创建——**无 Type 键**：匹配是类型模式判定（is TViewModel，
/// 编译期静态类型检查），创建是 IoC 工厂（GetRequiredService&lt;TView&gt;）。
/// 实例在 Build 前创建，成员仅供 ViewRegistry（同程序集）查询。
/// </summary>
public sealed class ViewMappingEntry
{
    /// <summary>
    /// 创建映射条目（仅 ViewMappingRegistry 内部使用）
    /// </summary>
    internal ViewMappingEntry(Func<object?, bool> match, Func<object?, IServiceProvider, Control> build)
    {
        Match = match;
        Build = build;
    }

    /// <summary>匹配委托（vm is TViewModel——类型模式，非反射）</summary>
    internal Func<object?, bool> Match { get; }

    /// <summary>创建委托（sp =&gt; sp.GetRequiredService&lt;TView&gt;()，IoC 创建）</summary>
    internal Func<object?, IServiceProvider, Control> Build { get; }
}
