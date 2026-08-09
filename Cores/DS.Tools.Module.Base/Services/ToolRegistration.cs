using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using DS.Tools.Core.Models;
using DS.Tools.Module.Base.Interfaces;

namespace DS.Tools.Module.Base.Services;

/// <summary>
/// 工具注册扩展方法（注册侧，统一注册入口）-
/// 每个方法一行完成「类型以 Transient 入容器」+「SubToolInfo（元数据 + 工厂 + View 映射）以单例入容器」，
/// 编译期强类型（泛型工厂 <c>sp =&gt; sp.GetRequiredService&lt;T&gt;()</c>），无 Type 键、零反射，AOT 兼容；
/// 容器构建后由 <see cref="IToolCatalog"/> 经集合注入查询（单条目类型，View 与子工具同源）。
/// </summary>
public static class ToolRegistration
{
    /// <summary>
    /// 注册子工具：ViewModel + View 均以 Transient 入容器 + SubToolInfo（元数据、IoC 工厂与 View 映射）以单例入容器。
    /// 元数据由 ViewModel 实现的 <see cref="ISubTool"/> 静态抽象成员提供——
    /// 编译期读取（<c>TViewModel.ModuleId</c> 经 constrained call），无需实例化、无需传参；
    /// View 映射为类型模式匹配 + IoC 工厂，子工具同时可渲染。
    /// 同一 ViewModel 重复注册时后注册者优先（覆盖语义，<see cref="ToolCatalog"/> 反转集合实现）。
    /// </summary>
    /// <typeparam name="TViewModel">子工具 ViewModel 类型（实现 <see cref="ISubTool"/>；以 Transient 注册，IoC 工厂强类型解析）</typeparam>
    /// <typeparam name="TView">View 类型（以 Transient 注册；经 IoC 工厂创建）</typeparam>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddSubTool<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TViewModel, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TView>(
        this IServiceCollection services)
        where TViewModel : ViewModelBase, ISubTool
        where TView : Control
    {
        services.AddTransient<TViewModel>();
        services.AddTransient<TView>();
        services.AddSingleton(new SubToolInfo(
            TViewModel.ModuleId,
            TViewModel.Id,
            TViewModel.Name,
            TViewModel.Icon,
            sp => sp.GetRequiredService<TViewModel>(),
            vm => vm is TViewModel,
            sp => sp.GetRequiredService<TView>()));

        return services;
    }

    /// <summary>
    /// 注册 View 映射（仅映射条目：应用级页面如主页，不属于任何模块，不参与子工具目录）。
    /// ViewModel + View 均以 Transient 入容器 + SubToolInfo（仅 View 映射）以单例入容器。
    /// 同一 ViewModel 重复注册时后注册者优先（覆盖语义）。
    /// </summary>
    /// <typeparam name="TViewModel">ViewModel 类型（以 Transient 注册）</typeparam>
    /// <typeparam name="TView">View 类型（以 Transient 注册；经 IoC 工厂创建）</typeparam>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddViewMapping<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TViewModel, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TView>(
        this IServiceCollection services)
        where TViewModel : class
        where TView : Control
    {
        services.AddTransient<TViewModel>();
        services.AddTransient<TView>();
        services.AddSingleton(new SubToolInfo(
            vm => vm is TViewModel,
            sp => sp.GetRequiredService<TView>()));

        return services;
    }
}
