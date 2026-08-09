using Microsoft.Extensions.DependencyInjection;
using DS.Tools.Core.Interfaces;
using DS.Tools.Core.Services;

namespace DS.Tools.Core.DI;

/// <summary>
/// 依赖注入扩展方法 - 显式注册所有服务，AOT 兼容。
/// 禁止运行时反射扫描程序集。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加应用核心服务（单例生命周期）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // ========================================
        // 应用服务（单例）
        // ========================================
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IClipboardService, ClipboardService>();

        return services;
    }
}