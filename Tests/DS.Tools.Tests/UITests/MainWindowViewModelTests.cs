using Xunit;
using FluentAssertions;
using Moq;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging.Abstractions;
using DS.Tools.ViewModels;
using DS.Tools.Module.Base.Interfaces;
using DS.Tools.Module.Base;
using DS.Tools.Core.Interfaces;
using DS.Tools.Core.Models;
using Avalonia.Styling;

namespace DS.Tools.Tests.UITests;

/// <summary>
/// MainWindowViewModel UI单元测试
/// 测试主窗口ViewModel的UI交互功能
/// </summary>
public sealed class MainWindowViewModelTests
{
    private readonly Mock<IToolRegistry> _mockToolRegistry;
    private readonly Mock<IThemeService> _mockThemeService;
    private readonly Mock<INavigationService> _mockNavigationService;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IToolModule> _mockTextModule;

    public MainWindowViewModelTests()
    {
        _mockToolRegistry = new Mock<IToolRegistry>();
        _mockThemeService = new Mock<IThemeService>();
        _mockNavigationService = new Mock<INavigationService>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockTextModule = new Mock<IToolModule>();

        // Setup default mock behaviors
        _mockTextModule.Setup(x => x.Id).Returns("text-tools");
        _mockTextModule.Setup(x => x.Name).Returns("文本工具");
        _mockTextModule.Setup(x => x.Icon).Returns("📝");
        _mockTextModule.Setup(x => x.Description).Returns("文本工具集");
        _mockTextModule.Setup(x => x.HasSubTools).Returns(true); // TextModule有子工具
        _mockTextModule.Setup(x => x.SubTools).Returns((IReadOnlyList<SubToolInfo>?)null);

        // IoC 创建：主/子 ViewModel 由模块工厂经 IServiceProvider 解析（与生产代码同构）
        _mockTextModule
            .Setup(x => x.CreateMainViewModel(It.IsAny<IServiceProvider>()))
            .Returns(new DummyViewModel());
        _mockTextModule
            .Setup(x => x.CreateSubToolViewModel("subtool1", It.IsAny<IServiceProvider>()))
            .Returns(new DummyViewModel());

        var tools = new List<IToolModule> { _mockTextModule.Object };
        _mockToolRegistry.Setup(x => x.Tools).Returns(tools);
        _mockToolRegistry.Setup(x => x.GetTool("text-tools")).Returns(_mockTextModule.Object);

        // 主页（应用级 DashboardViewModel）经 DI 解析——与生产代码同构
        _mockServiceProvider
            .Setup(x => x.GetService(typeof(DashboardViewModel)))
            .Returns(new DashboardViewModel(
                _mockToolRegistry.Object,
                _mockNavigationService.Object,
                NullLogger<DashboardViewModel>.Instance));

        _mockThemeService.Setup(x => x.CurrentTheme).Returns(ThemeVariant.Light);
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        // Act
        var viewModel = new MainWindowViewModel(
            _mockToolRegistry.Object,
            _mockThemeService.Object,
            _mockNavigationService.Object,
            _mockServiceProvider.Object);

        // Assert
        viewModel.Tools.Should().HaveCount(1);
        viewModel.IsPaneOpen.Should().BeFalse(); // 侧边栏默认收起
        viewModel.CurrentThemeIcon.Should().Be("🌙");
    }

    [Fact]
    public void Constructor_ShouldSubscribeToNavigationEvents()
    {
        // Act
        var viewModel = new MainWindowViewModel(
            _mockToolRegistry.Object,
            _mockThemeService.Object,
            _mockNavigationService.Object,
            _mockServiceProvider.Object);

        // Assert
        _mockNavigationService.VerifyAdd(x => x.NavigationChanged += It.IsAny<System.Action<IToolModule?, string?>>(), Times.Once);
    }

    [Fact]
    public void TogglePaneCommand_ShouldToggleIsPaneOpen()
    {
        // Arrange
        var viewModel = new MainWindowViewModel(
            _mockToolRegistry.Object,
            _mockThemeService.Object,
            _mockNavigationService.Object,
            _mockServiceProvider.Object);
        var initialState = viewModel.IsPaneOpen;

        // Act
        viewModel.TogglePaneCommand.Execute(null);

        // Assert
        viewModel.IsPaneOpen.Should().Be(!initialState);
    }

    [Fact]
    public void ToggleThemeCommand_ShouldSwitchTheme()
    {
        // Arrange
        var viewModel = new MainWindowViewModel(
            _mockToolRegistry.Object,
            _mockThemeService.Object,
            _mockNavigationService.Object,
            _mockServiceProvider.Object);
        _mockThemeService.Setup(x => x.CurrentTheme).Returns(ThemeVariant.Dark);

        // Act
        viewModel.ToggleThemeCommand.Execute(null);

        // Assert
        _mockThemeService.Verify(x => x.SetTheme(It.IsAny<ThemeVariant>()), Times.Once);
    }

    [Fact]
    public void ToggleThemeCommand_ShouldUpdateIcon()
    {
        // Arrange
        bool themeToggled = false;
        _mockThemeService.Setup(x => x.CurrentTheme).Returns(() =>
            themeToggled ? ThemeVariant.Dark : ThemeVariant.Light);

        _mockThemeService.Setup(x => x.SetTheme(It.IsAny<ThemeVariant>()))
            .Callback(() => themeToggled = true);

        var viewModel = new MainWindowViewModel(
            _mockToolRegistry.Object,
            _mockThemeService.Object,
            _mockNavigationService.Object,
            _mockServiceProvider.Object);

        // Act
        viewModel.ToggleThemeCommand.Execute(null);

        // Assert
        viewModel.CurrentThemeIcon.Should().Be("☀️"); // Dark主题应该显示太阳图标
    }

    [Fact]
    public void SelectSubToolCommand_WithValidSubTool_ShouldNavigate()
    {
        // Arrange
        var viewModel = new MainWindowViewModel(
            _mockToolRegistry.Object,
            _mockThemeService.Object,
            _mockNavigationService.Object,
            _mockServiceProvider.Object);
        // SubToolInfo 自带 ModuleId：SelectSubTool 按它定位所属模块并拼接完整导航 ID
        var subTool = new SubToolInfo("text-tools", "test1", "Test Tool 1", "🔧", _ => new DummyViewModel());

        // Act
        viewModel.SelectSubToolCommand.Execute(subTool);

        // Assert
        _mockNavigationService.Verify(x => x.NavigateTo("text-tools:test1"), Times.Once);
    }

    [Fact]
    public void SelectSubToolCommand_WithSubToolFromOtherModule_ShouldNavigateToOwningModule()
    {
        // Arrange（回归：多模块下子工具按钮来自任意模块，
        // 导航 ID 必须以所属模块为准，而非当前活动模块）
        var viewModel = new MainWindowViewModel(
            _mockToolRegistry.Object,
            _mockThemeService.Object,
            _mockNavigationService.Object,
            _mockServiceProvider.Object);
        var gitSubTool = new SubToolInfo("git-tools", "git-log", "Git 日志", "📜", _ => new DummyViewModel());
        var gitModule = new Mock<IToolModule>();
        gitModule.Setup(x => x.Id).Returns("git-tools");
        _mockToolRegistry.Setup(x => x.Tools).Returns(new List<IToolModule> { _mockTextModule.Object, gitModule.Object });

        // Act
        viewModel.SelectSubToolCommand.Execute(gitSubTool);

        // Assert（应路由到 git-tools:git-log，而非错误的 text-tools:git-log）
        _mockNavigationService.Verify(x => x.NavigateTo("git-tools:git-log"), Times.Once);
    }

    [Fact]
    public void NavigateToHomeCommand_ShouldShowHomeViewModel()
    {
        // Arrange
        var viewModel = new MainWindowViewModel(
            _mockToolRegistry.Object,
            _mockThemeService.Object,
            _mockNavigationService.Object,
            _mockServiceProvider.Object);

        // 启动即主页
        viewModel.ActiveToolViewModel.Should().BeOfType<DashboardViewModel>();
        var home = viewModel.ActiveToolViewModel;

        // 导航到子工具后返回主页（应复用同一实例，缓存命中）
        _mockNavigationService.Raise(x => x.NavigationChanged += null, _mockTextModule.Object, "subtool1");
        viewModel.ActiveToolViewModel.Should().BeOfType<DummyViewModel>();

        viewModel.NavigateToHomeCommand.Execute(null);
        viewModel.ActiveToolViewModel.Should().BeSameAs(home);
    }

    [Fact]
    public void NavigationChanged_ShouldUpdateActiveToolViewModel()
    {
        // Arrange
        var viewModel = new MainWindowViewModel(
            _mockToolRegistry.Object,
            _mockThemeService.Object,
            _mockNavigationService.Object,
            _mockServiceProvider.Object);
        var dummyViewModel = new DummyViewModel();
        _mockTextModule.Setup(x => x.CreateMainViewModel(It.IsAny<IServiceProvider>())).Returns(dummyViewModel);

        // Act
        _mockNavigationService.Raise(x => x.NavigationChanged += null, _mockTextModule.Object, (string?)null!);

        // Assert
        viewModel.ActiveToolViewModel.Should().Be(dummyViewModel);
    }

    [Fact]
    public void NavigationChanged_WithSubTool_ShouldUseSubToolFactory()
    {
        // Arrange
        var viewModel = new MainWindowViewModel(
            _mockToolRegistry.Object,
            _mockThemeService.Object,
            _mockNavigationService.Object,
            _mockServiceProvider.Object);
        var subToolViewModel = new DummyViewModel();
        _mockTextModule
            .Setup(x => x.CreateSubToolViewModel("subtool1", It.IsAny<IServiceProvider>()))
            .Returns(subToolViewModel);

        // Act
        _mockNavigationService.Raise(x => x.NavigationChanged += null, _mockTextModule.Object, "subtool1");

        // Assert
        viewModel.ActiveToolViewModel.Should().Be(subToolViewModel);
    }

    [Fact]
    public void NavigationChanged_WithUnknownSubTool_ShouldFallBackToMainViewModel()
    {
        // Arrange
        var viewModel = new MainWindowViewModel(
            _mockToolRegistry.Object,
            _mockThemeService.Object,
            _mockNavigationService.Object,
            _mockServiceProvider.Object);
        var mainViewModel = new DummyViewModel();
        _mockTextModule
            .Setup(x => x.CreateSubToolViewModel("unknown", It.IsAny<IServiceProvider>()))
            .Returns((ViewModelBase?)null);
        _mockTextModule
            .Setup(x => x.CreateMainViewModel(It.IsAny<IServiceProvider>()))
            .Returns(mainViewModel);

        // Act
        _mockNavigationService.Raise(x => x.NavigationChanged += null, _mockTextModule.Object, "unknown");

        // Assert
        viewModel.ActiveToolViewModel.Should().Be(mainViewModel);
    }

    [Fact]
    public void IsPaneOpen_DefaultValue_ShouldBeFalse()
    {
        // Arrange & Act
        var viewModel = new MainWindowViewModel(
            _mockToolRegistry.Object,
            _mockThemeService.Object,
            _mockNavigationService.Object,
            _mockServiceProvider.Object);

        // Assert（侧边栏默认收起）
        viewModel.IsPaneOpen.Should().BeFalse();
    }

    [Fact]
    public void CurrentThemeIcon_DarkTheme_ShouldShowSunIcon()
    {
        // Arrange
        _mockThemeService.Setup(x => x.CurrentTheme).Returns(ThemeVariant.Dark);

        // Act
        var viewModel = new MainWindowViewModel(
            _mockToolRegistry.Object,
            _mockThemeService.Object,
            _mockNavigationService.Object,
            _mockServiceProvider.Object);

        // Assert
        viewModel.CurrentThemeIcon.Should().Be("☀️");
    }

    [Fact]
    public void CurrentThemeIcon_LightTheme_ShouldShowMoonIcon()
    {
        // Arrange
        _mockThemeService.Setup(x => x.CurrentTheme).Returns(ThemeVariant.Light);

        // Act
        var viewModel = new MainWindowViewModel(
            _mockToolRegistry.Object,
            _mockThemeService.Object,
            _mockNavigationService.Object,
            _mockServiceProvider.Object);

        // Assert
        viewModel.CurrentThemeIcon.Should().Be("🌙");
    }

    [Fact]
    public void NavigationChanged_TwiceWithSameKey_ShouldReuseCachedViewModel()
    {
        // Arrange（每次工厂调用返回新实例，用于区分缓存命中与否）
        var registry = new Mock<IToolRegistry>();
        var theme = new Mock<IThemeService>();
        theme.Setup(x => x.CurrentTheme).Returns(ThemeVariant.Light);
        var nav = new Mock<INavigationService>();
        var sp = new Mock<IServiceProvider>();
        var module = new Mock<IToolModule>();
        module.Setup(x => x.Id).Returns("m1");
        module.Setup(x => x.CreateMainViewModel(It.IsAny<IServiceProvider>())).Returns(() => new DummyViewModel());
        registry.Setup(x => x.Tools).Returns([]);
        registry.Setup(x => x.GetTool("m1")).Returns(module.Object);
        sp.Setup(x => x.GetService(typeof(DashboardViewModel)))
            .Returns(new DashboardViewModel(registry.Object, nav.Object, NullLogger<DashboardViewModel>.Instance));

        var viewModel = new MainWindowViewModel(registry.Object, theme.Object, nav.Object, sp.Object);

        // Act
        nav.Raise(x => x.NavigationChanged += null, module.Object, (string?)null!);
        var first = viewModel.ActiveToolViewModel;
        nav.Raise(x => x.NavigationChanged += null, module.Object, (string?)null!);
        var second = viewModel.ActiveToolViewModel;

        // Assert
        second.Should().BeSameAs(first);
        module.Verify(x => x.CreateMainViewModel(It.IsAny<IServiceProvider>()), Times.Once);
    }

    [Fact]
    public void NavigationChanged_WithDifferentKeys_ShouldCreateSeparateViewModels()
    {
        // Arrange
        var registry = new Mock<IToolRegistry>();
        var theme = new Mock<IThemeService>();
        theme.Setup(x => x.CurrentTheme).Returns(ThemeVariant.Light);
        var nav = new Mock<INavigationService>();
        var sp = new Mock<IServiceProvider>();
        var module = new Mock<IToolModule>();
        module.Setup(x => x.Id).Returns("m1");
        module.Setup(x => x.CreateMainViewModel(It.IsAny<IServiceProvider>())).Returns(() => new DummyViewModel());
        module.Setup(x => x.CreateSubToolViewModel("sub1", It.IsAny<IServiceProvider>())).Returns(() => new DummyViewModel());
        registry.Setup(x => x.Tools).Returns([]);
        registry.Setup(x => x.GetTool("m1")).Returns(module.Object);
        sp.Setup(x => x.GetService(typeof(DashboardViewModel)))
            .Returns(new DashboardViewModel(registry.Object, nav.Object, NullLogger<DashboardViewModel>.Instance));

        var viewModel = new MainWindowViewModel(registry.Object, theme.Object, nav.Object, sp.Object);

        // Act：主视图 → 子工具视图 → 返回主视图
        nav.Raise(x => x.NavigationChanged += null, module.Object, (string?)null!);
        var mainVm = viewModel.ActiveToolViewModel;
        nav.Raise(x => x.NavigationChanged += null, module.Object, "sub1");
        var subVm = viewModel.ActiveToolViewModel;
        nav.Raise(x => x.NavigationChanged += null, module.Object, (string?)null!);

        // Assert
        subVm.Should().NotBeSameAs(mainVm);
        viewModel.ActiveToolViewModel.Should().BeSameAs(mainVm); // 返回时复用缓存
    }

    /// <summary>
    /// 测试用的简单ViewModel
    /// </summary>
    private class DummyViewModel : ViewModelBase
    {
        public string? TestProperty { get; set; } = "Test";
    }
}
