using Avalonia.Controls;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DS.Tools.Module.Base.Services;

namespace DS.Tools.Tests.UnitTests;

/// <summary>
/// View 注册表单元测试 - AddViewMapping 一行注册（VM+View 入容器 + 类型模式映射）+ View 经 DI 容器 IoC 创建。
/// 覆盖：VM/View 容器注册/映射→新实例/未注册 null/覆盖注册/派生 VM 匹配/模板桥接（Match/Build 委托）/View 依赖注入。
/// </summary>
public sealed class ViewRegistryTests
{
    private class SampleViewModel { }

    private sealed class DerivedViewModel : SampleViewModel { }

    private sealed class OtherViewModel { }

    private sealed class SampleDependency { }

    private sealed class SampleView : UserControl { }

    private sealed class OtherView : UserControl { }

    /// <summary>
    /// 带构造依赖的 View（验证 IoC 创建：依赖由 DI 容器注入）
    /// </summary>
    private sealed class InjectedView(SampleDependency dependency) : UserControl
    {
        public SampleDependency Dependency { get; } = dependency;
    }

    /// <summary>
    /// 构建容器 + 查询注册表（与生产同构：映射经扩展方法注册进容器，ViewRegistry 集合注入消费）
    /// </summary>
    private static ViewRegistry CreateRegistry(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        var sp = services.BuildServiceProvider();
        return new ViewRegistry(sp.GetRequiredService<IEnumerable<ViewMappingEntry>>(), sp);
    }

    [Fact]
    public void AddViewMapping_RegistersViewModelAndViewIntoContainer()
    {
        // Arrange & Act（AddViewMapping 同时注册 VM 与 View）
        var services = new ServiceCollection();
        services.AddViewMapping<SampleViewModel, SampleView>();
        var sp = services.BuildServiceProvider();

        // Assert（Transient：均可解析）
        sp.GetRequiredService<SampleViewModel>().Should().NotBeNull();
        sp.GetRequiredService<SampleView>().Should().NotBeNull();
    }

    [Fact]
    public void AddViewMapping_ThenGetView_ReturnsNewInstanceEachTime()
    {
        // Arrange（AddViewMapping 一行完成容器注册 + 映射声明；Transient：每次解析新实例）
        var registry = CreateRegistry(services =>
            services.AddViewMapping<SampleViewModel, SampleView>());

        // Act
        var view1 = registry.GetView(new SampleViewModel());
        var view2 = registry.GetView(new SampleViewModel());

        // Assert
        view1.Should().BeOfType<SampleView>();
        view2.Should().BeOfType<SampleView>();
        view1.Should().NotBeSameAs(view2);
    }

    [Fact]
    public void GetView_DerivedViewModel_MatchesBaseMapping()
    {
        // Arrange（is 类型模式匹配：无 Type 键，派生 VM 命中基类映射——字典 Type 键做不到）
        var registry = CreateRegistry(services =>
            services.AddViewMapping<SampleViewModel, SampleView>());

        // Act & Assert
        registry.IsRegistered(new DerivedViewModel()).Should().BeTrue();
        registry.GetView(new DerivedViewModel()).Should().BeOfType<SampleView>();
    }

    [Fact]
    public void GetView_ForNullOrUnregistered_ReturnsNull()
    {
        // Arrange
        var registry = CreateRegistry(_ => { });

        // Act & Assert
        registry.GetView(null).Should().BeNull();
        registry.GetView(new OtherViewModel()).Should().BeNull();
        registry.IsRegistered(null).Should().BeFalse();
        registry.IsRegistered(new OtherViewModel()).Should().BeFalse();
    }

    [Fact]
    public void AddViewMapping_SameViewModelTwice_LastRegistrationWins()
    {
        // Arrange
        var registry = CreateRegistry(services =>
        {
            services.AddViewMapping<SampleViewModel, SampleView>();
            services.AddViewMapping<SampleViewModel, OtherView>();
        });

        // Act
        var view = registry.GetView(new SampleViewModel());

        // Assert
        view.Should().BeOfType<OtherView>();
    }

    [Fact]
    public void AddViewMapping_ViewWithDependencies_ResolvesDependenciesViaIoC()
    {
        // Arrange（核心：View 依赖经 DI 容器注入，而非 new TView() 直建）
        var registry = CreateRegistry(services =>
        {
            services.AddSingleton<SampleDependency>();
            services.AddViewMapping<SampleViewModel, InjectedView>();
        });

        // Act
        var view = registry.GetView(new SampleViewModel()) as InjectedView;

        // Assert
        view.Should().NotBeNull();
        view!.Dependency.Should().NotBeNull("依赖应经 DI 容器注入 View 构造");
    }

    [Fact]
    public void DataTemplate_MatchAndBuild_DelegatesToRegistry()
    {
        // Arrange
        var registry = CreateRegistry(services =>
            services.AddViewMapping<SampleViewModel, SampleView>());
        var template = new ViewRegistryDataTemplate(registry);
        var vm = new SampleViewModel();

        // Act & Assert：已注册 VM → Match true / Build 返回对应 View
        template.Match(vm).Should().BeTrue();
        template.Build(vm).Should().BeOfType<SampleView>();

        // 未注册 VM 与 null → Match false / Build null（不命中即跳过，等价于无模板）
        template.Match(new OtherViewModel()).Should().BeFalse();
        template.Match(null).Should().BeFalse();
        template.Build(new OtherViewModel()).Should().BeNull();
        template.Build(null).Should().BeNull();
    }
}
