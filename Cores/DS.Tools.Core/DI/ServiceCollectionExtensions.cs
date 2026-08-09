using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DS.Tools.Core.Interfaces;
using DS.Tools.Core.Services;
using DS.Tools.Core.Infrastructure;
using DS.Tools.Core.Infrastructure.Logging;
using System.Runtime.CompilerServices;

namespace DS.Tools.Core.DI;

/// <summary>
/// 依赖注入扩展方法 - 显式注册所有服务，AOT 兼容。
/// 禁止运行时反射扫描程序集。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加核心基础设施服务（单例生命周期）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合（链式调用）</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        // ========================================
        // 基础设施服务（单例）
        // ========================================
        services.AddSingleton<ILoggerFactory, SerilogLoggerFactory>();
        services.AddSingleton<IEventAggregator, EventAggregator>();

        return services;
    }

    /// <summary>
    /// 添加应用核心服务（单例生命周期）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合（链式调用）</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // ========================================
        // 应用服务（单例）
        // ========================================
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<IClipboardService, ClipboardService>();

        return services;
    }
}