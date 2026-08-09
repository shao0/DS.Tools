using Microsoft.Extensions.DependencyInjection;
using DS.Tools.Module.Base.Interfaces;
using DS.Tools.Module.Base.Services;
using System.Runtime.CompilerServices;

namespace DS.Tools.Module.Base.DI;

/// <summary>
/// 模块化依赖注入扩展方法 - 显式注册模块相关服务，AOT 兼容。
/// </summary>
public static class ModuleServiceCollectionExtensions
{
    /// <summary>
    /// 添加模块化服务（单例生命周期）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合（链式调用）</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IServiceCollection AddModuleServices(this IServiceCollection services)
    {
        // ========================================
        // 模块化服务（单例）
        // ========================================
        services.AddSingleton<IToolRegistry, ToolRegistry>();
        services.AddSingleton<INavigationService, NavigationService>();

        return services;
    }
}