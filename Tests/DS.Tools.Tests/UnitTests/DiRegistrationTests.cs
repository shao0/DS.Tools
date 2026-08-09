using Xunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using DS.Tools.Core.DI;
using DS.Tools.Core.Interfaces;
using DS.Tools.Module.Base.DI;
using DS.Tools.Module.Base.Interfaces;

namespace DS.Tools.Tests.UnitTests;

/// <summary>
/// DI 注册完整性测试：AddApplicationServices / AddModuleServices 显式注册的服务
/// 均可解析且为单例（与组合根一致，防注册遗漏回归）
/// </summary>
public sealed class DiRegistrationTests
{
    [Fact]
    public void AddApplicationServices_AllServicesResolvable()
    {
        // Arrange
        var provider = new ServiceCollection()
            .AddLogging() // ClipboardService/FolderPickerService 依赖 ILogger<T>
            .AddApplicationServices()
            .BuildServiceProvider();

        // Act & Assert
        provider.GetRequiredService<IThemeService>().Should().NotBeNull();
        provider.GetRequiredService<IClipboardService>().Should().NotBeNull();
        provider.GetRequiredService<IFolderPickerService>().Should().NotBeNull();
    }

    [Fact]
    public void AddApplicationServices_ServicesAreSingleton()
    {
        // Arrange
        var provider = new ServiceCollection()
            .AddLogging()
            .AddApplicationServices()
            .BuildServiceProvider();

        // Act & Assert
        provider.GetRequiredService<IThemeService>()
            .Should().BeSameAs(provider.GetRequiredService<IThemeService>());
        provider.GetRequiredService<IClipboardService>()
            .Should().BeSameAs(provider.GetRequiredService<IClipboardService>());
        provider.GetRequiredService<IFolderPickerService>()
            .Should().BeSameAs(provider.GetRequiredService<IFolderPickerService>());
    }

    [Fact]
    public void AddModuleServices_AllServicesResolvable()
    {
        // Arrange
        var provider = new ServiceCollection()
            .AddModuleServices()
            .BuildServiceProvider();

        // Act & Assert
        provider.GetRequiredService<IToolRegistry>().Should().NotBeNull();
        provider.GetRequiredService<INavigationService>().Should().NotBeNull();
        provider.GetRequiredService<IToolCatalog>().Should().NotBeNull();
    }

    [Fact]
    public void AddModuleServices_ServicesAreSingleton()
    {
        // Arrange
        var provider = new ServiceCollection()
            .AddModuleServices()
            .BuildServiceProvider();

        // Act & Assert
        provider.GetRequiredService<IToolRegistry>()
            .Should().BeSameAs(provider.GetRequiredService<IToolRegistry>());
        provider.GetRequiredService<INavigationService>()
            .Should().BeSameAs(provider.GetRequiredService<INavigationService>());
        provider.GetRequiredService<IToolCatalog>()
            .Should().BeSameAs(provider.GetRequiredService<IToolCatalog>());
    }

    [Fact]
    public void AddApplicationAndModuleServices_ComposeWithoutConflict()
    {
        // 组合根同时调用两个扩展不应冲突（模拟 App 配置）
        // Arrange
        var provider = new ServiceCollection()
            .AddLogging()
            .AddApplicationServices()
            .AddModuleServices()
            .BuildServiceProvider();

        // Act & Assert
        provider.GetRequiredService<IThemeService>().Should().NotBeNull();
        provider.GetRequiredService<IToolRegistry>().Should().NotBeNull();
        provider.GetRequiredService<INavigationService>().Should().NotBeNull();
        provider.GetRequiredService<IToolCatalog>().Should().NotBeNull();
    }
}
