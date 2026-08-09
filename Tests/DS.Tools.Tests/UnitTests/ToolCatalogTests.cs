using Avalonia.Controls;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DS.Tools.Core.DI;
using DS.Tools.Core.Models;
using DS.Tools.Module.Base;
using DS.Tools.Module.Base.Interfaces;
using DS.Tools.Module.Base.Services;
using DS.Tools.Module.Text;
using DS.Tools.Module.Text.ViewModels;

namespace DS.Tools.Tests.UnitTests;

/// <summary>
/// 统一工具目录（ToolCatalog + ToolRegistration）单元测试 -
/// AddSubTool/AddViewMapping 一行注册（类型入容器 + SubToolInfo 单条目含元数据/工厂/View 映射入容器）+
/// IToolCatalog 查询（View 映射：类型模式匹配/覆盖/派生命中/IoC 创建；子工具：按模块过滤/按 ID 查询）+
/// ToolRegistry 挂载目录到模块基类 + 模板桥接（Match/Build 委托）。
/// </summary>
public sealed class ToolCatalogTests
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

    /// <summary>测试用子工具 VM（元数据经 ISubTool 静态抽象接口声明，供 AddSubTool&lt;T,TView&gt;() 编译期读取）</summary>
    private sealed class TestViewModel : ViewModelBase, ISubTool
    {
        static string ISubTool.ModuleId => "m1";
        static string ISubTool.Id => "s1";
        static string ISubTool.Name => "工具一";
        static string ISubTool.Icon => "🔧";
    }

    private sealed class TestViewModelA : ViewModelBase, ISubTool
    {
        static string ISubTool.ModuleId => "m1";
        static string ISubTool.Id => "s1";
        static string ISubTool.Name => "A";
        static string ISubTool.Icon => "🔧";
    }

    private sealed class TestViewModelB : ViewModelBase, ISubTool
    {
        static string ISubTool.ModuleId => "m1";
        static string ISubTool.Id => "s2";
        static string ISubTool.Name => "B";
        static string ISubTool.Icon => "🔨";
    }

    private sealed class TestViewModelC : ViewModelBase, ISubTool
    {
        static string ISubTool.ModuleId => "m2";
        static string ISubTool.Id => "s3";
        static string ISubTool.Name => "C";
        static string ISubTool.Icon => "⚙️";
    }

    /// <summary>
    /// 构建容器 + 查询目录（与生产同构：注册经扩展方法入容器，ToolCatalog 单集合注入消费）
    /// </summary>
    private static ToolCatalog CreateCatalog(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        var sp = services.BuildServiceProvider();
        return new ToolCatalog(sp.GetRequiredService<IEnumerable<SubToolInfo>>(), sp);
    }

    // ==================== View 映射注册（AddViewMapping）====================

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
        var catalog = CreateCatalog(services =>
            services.AddViewMapping<SampleViewModel, SampleView>());

        // Act
        var view1 = catalog.GetView(new SampleViewModel());
        var view2 = catalog.GetView(new SampleViewModel());

        // Assert
        view1.Should().BeOfType<SampleView>();
        view2.Should().BeOfType<SampleView>();
        view1.Should().NotBeSameAs(view2);
    }

    [Fact]
    public void GetView_DerivedViewModel_MatchesBaseMapping()
    {
        // Arrange（is 类型模式匹配：无 Type 键，派生 VM 命中基类映射——字典 Type 键做不到）
        var catalog = CreateCatalog(services =>
            services.AddViewMapping<SampleViewModel, SampleView>());

        // Act & Assert
        catalog.IsRegistered(new DerivedViewModel()).Should().BeTrue();
        catalog.GetView(new DerivedViewModel()).Should().BeOfType<SampleView>();
    }

    [Fact]
    public void GetView_ForNullOrUnregistered_ReturnsNull()
    {
        // Arrange
        var catalog = CreateCatalog(_ => { });

        // Act & Assert
        catalog.GetView(null).Should().BeNull();
        catalog.GetView(new OtherViewModel()).Should().BeNull();
        catalog.IsRegistered(null).Should().BeFalse();
        catalog.IsRegistered(new OtherViewModel()).Should().BeFalse();
    }

    [Fact]
    public void AddViewMapping_SameViewModelTwice_LastRegistrationWins()
    {
        // Arrange
        var catalog = CreateCatalog(services =>
        {
            services.AddViewMapping<SampleViewModel, SampleView>();
            services.AddViewMapping<SampleViewModel, OtherView>();
        });

        // Act
        var view = catalog.GetView(new SampleViewModel());

        // Assert
        view.Should().BeOfType<OtherView>();
    }

    [Fact]
    public void AddViewMapping_ViewWithDependencies_ResolvesDependenciesViaIoC()
    {
        // Arrange（核心：View 依赖经 DI 容器注入，而非 new TView() 直建）
        var catalog = CreateCatalog(services =>
        {
            services.AddSingleton<SampleDependency>();
            services.AddViewMapping<SampleViewModel, InjectedView>();
        });

        // Act
        var view = catalog.GetView(new SampleViewModel()) as InjectedView;

        // Assert
        view.Should().NotBeNull();
        view!.Dependency.Should().NotBeNull("依赖应经 DI 容器注入 View 构造");
    }

    [Fact]
    public void DataTemplate_MatchAndBuild_DelegatesToCatalog()
    {
        // Arrange
        var catalog = CreateCatalog(services =>
            services.AddViewMapping<SampleViewModel, SampleView>());
        var template = new ViewRegistryDataTemplate(catalog);
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

    // ==================== 子工具注册（AddSubTool）====================

    [Fact]
    public void AddSubTool_RegistersViewModelAndCatalogEntry()
    {
        // Arrange & Act（AddSubTool 一行同时注册 VM/View 与 SubToolInfo 单例；元数据来自 VM 的 ISubTool 接口声明）
        var services = new ServiceCollection();
        services.AddSubTool<TestViewModel, SampleView>();
        var sp = services.BuildServiceProvider();

        // Assert（VM/View 可解析；目录条目元数据与接口声明一致）
        sp.GetRequiredService<TestViewModel>().Should().NotBeNull();
        sp.GetRequiredService<SampleView>().Should().NotBeNull();
        var catalog = new ToolCatalog(sp.GetRequiredService<IEnumerable<SubToolInfo>>(), sp);
        var entry = catalog.GetSubTools("m1").Should().ContainSingle().Which;
        entry.Id.Should().Be("s1");
        entry.Name.Should().Be("工具一");
        entry.Icon.Should().Be("🔧");
        entry.ModuleId.Should().Be("m1");
    }

    [Fact]
    public void AddSubTool_OneLine_AlsoRegistersViewMapping()
    {
        // Arrange（合并核心收益：AddSubTool 一行完成子工具 + View 映射，无独立 AddViewMapping 调用）
        var catalog = CreateCatalog(services =>
            services.AddSubTool<TestViewModel, SampleView>());

        // Act & Assert（同一条目经目录即可查询子工具，又可渲染 View）
        catalog.GetSubTool("m1", "s1").Should().NotBeNull();
        catalog.IsRegistered(new TestViewModel()).Should().BeTrue();
        catalog.GetView(new TestViewModel()).Should().BeOfType<SampleView>();
    }

    [Fact]
    public void GetSubTools_ReturnsOnlySubToolsOfModule()
    {
        // Arrange（多模块混合注册，按 ModuleId 过滤）
        var catalog = CreateCatalog(services =>
        {
            services.AddSubTool<TestViewModelA, SampleView>();
            services.AddSubTool<TestViewModelB, SampleView>();
            services.AddSubTool<TestViewModelC, SampleView>();
        });

        // Act & Assert
        catalog.GetSubTools("m1").Should().HaveCount(2);
        catalog.GetSubTools("m1").Select(s => s.Id).Should().BeEquivalentTo(["s1", "s2"]);
        catalog.GetSubTools("m2").Should().ContainSingle().Which.Id.Should().Be("s3");
    }

    [Fact]
    public void GetSubTools_UnknownModule_ReturnsEmpty()
    {
        // Arrange
        var catalog = CreateCatalog(services => services.AddSubTool<TestViewModel, SampleView>());

        // Act & Assert
        catalog.GetSubTools("nope").Should().BeEmpty();
    }

    [Fact]
    public void GetSubTool_ById_ReturnsEntry()
    {
        // Arrange
        var catalog = CreateCatalog(services =>
        {
            services.AddSubTool<TestViewModelA, SampleView>();
            services.AddSubTool<TestViewModelB, SampleView>();
        });

        // Act & Assert（按模块 + 子工具 ID 精确定位）
        var result = catalog.GetSubTool("m1", "s2");
        result.Should().NotBeNull();
        result!.Name.Should().Be("B");
    }

    [Fact]
    public void GetSubTool_UnknownId_ReturnsNull()
    {
        // Arrange
        var catalog = CreateCatalog(services => services.AddSubTool<TestViewModel, SampleView>());

        // Act & Assert（未知子工具 ID / 未知模块均返回 null）
        catalog.GetSubTool("m1", "nope").Should().BeNull();
        catalog.GetSubTool("nope", "s1").Should().BeNull();
    }

    [Fact]
    public void SubToolInfo_GetFullNavigationId_UsesOwnModuleId()
    {
        // Arrange（SubToolInfo 自带 ModuleId，无需外部再传）
        var catalog = CreateCatalog(services => services.AddSubTool<TestViewModel, SampleView>());

        // Act & Assert
        catalog.GetSubTool("m1", "s1")!.GetFullNavigationId().Should().Be("m1:s1");
    }

    [Fact]
    public void Module_Register_ThenToolRegistryRegister_AttachesCatalogToModule()
    {
        // Arrange（集成：真实 TextModule 经容器注册，元数据在 Register 阶段入容器）
        var services = new ServiceCollection();
        services.AddLogging(); // ILogger<T> 由标准 AddLogging 提供
        services.AddApplicationServices(); // 子工具 VM 所需 Core 服务（如 IClipboardService）
        var module = new TextModule();
        module.Register(services);
        services.AddSingleton(module);
        services.AddSingleton<IToolCatalog, ToolCatalog>();
        services.AddSingleton<IToolRegistry, ToolRegistry>();
        var sp = services.BuildServiceProvider();

        // Act（ToolRegistry.Register 挂载统一目录到模块基类——生产同构路径）
        var registry = sp.GetRequiredService<IToolRegistry>();
        registry.Register(module);

        // Assert（SubTools/HasSubTools 即刻可用；CreateSubToolViewModel 经 IoC 工厂创建）
        module.SubTools.Should().HaveCount(6);
        module.HasSubTools.Should().BeTrue();
        module.SubTools!.Select(s => s.Id).Should()
            .BeEquivalentTo([TextModule.ToolIds.JsonFormatter, TextModule.ToolIds.Base64Converter, TextModule.ToolIds.ColorConverter, TextModule.ToolIds.PasswordGenerator, TextModule.ToolIds.TextHasher, TextModule.ToolIds.TimestampConverter]);

        module.CreateSubToolViewModel(TextModule.ToolIds.JsonFormatter, sp)
            .Should().BeOfType<JsonFormatterViewModel>();
        module.CreateSubToolViewModel("unknown", sp).Should().BeNull();
    }
}
