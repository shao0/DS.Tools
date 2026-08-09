using Xunit;
using FluentAssertions;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using DS.Tools.Core.DI;
using DS.Tools.Core.Interfaces;

namespace DS.Tools.Tests.UnitTests;

/// <summary>
/// ThemeService 单元测试（经 DI 解析 internal 实现）：
/// 主题状态管理（无 Avalonia 平台时仅维护字段，Application.Current 为空不触碰应用）
/// </summary>
public sealed class ThemeServiceTests
{
    private static IThemeService CreateService()
        => new ServiceCollection()
            .AddApplicationServices()
            .BuildServiceProvider()
            .GetRequiredService<IThemeService>();

    [Fact]
    public void Constructor_DefaultTheme_IsDefaultVariant()
    {
        // Arrange & Act
        var service = CreateService();

        // Assert
        service.CurrentTheme.Should().Be(ThemeVariant.Default);
    }

    [Fact]
    public void SetTheme_Dark_UpdatesCurrentTheme()
    {
        // Arrange
        var service = CreateService();

        // Act
        service.SetTheme(ThemeVariant.Dark);

        // Assert
        service.CurrentTheme.Should().Be(ThemeVariant.Dark);
    }

    [Fact]
    public void SetTheme_Light_AfterDark_UpdatesToLight()
    {
        // Arrange
        var service = CreateService();
        service.SetTheme(ThemeVariant.Dark);

        // Act
        service.SetTheme(ThemeVariant.Light);

        // Assert
        service.CurrentTheme.Should().Be(ThemeVariant.Light);
    }

    [Fact]
    public void SetTheme_SameTheme_IsIdempotent()
    {
        // Arrange
        var service = CreateService();
        service.SetTheme(ThemeVariant.Dark);
        var current = service.CurrentTheme;

        // Act
        service.SetTheme(ThemeVariant.Dark);

        // Assert
        service.CurrentTheme.Should().Be(current);
    }

    [Fact]
    public void SetTheme_Null_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateService();

        // Act
        var act = () => service.SetTheme(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
