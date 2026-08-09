using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using DS.Tools.Module.Base.Interfaces;
using DS.Tools.Module.Base.Services;

namespace DS.Tools.Tests.UnitTests;

/// <summary>
/// NavigationService 单元测试
/// 测试导航服务的核心功能
/// </summary>
public sealed class NavigationServiceTests
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IToolRegistry> _mockToolRegistry;
    private readonly Mock<IToolModule> _mockModule1;
    private readonly Mock<IToolModule> _mockModule2;

    public NavigationServiceTests()
    {
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockToolRegistry = new Mock<IToolRegistry>();

        _mockModule1 = new Mock<IToolModule>();
        _mockModule1.Setup(x => x.Id).Returns("module1");
        _mockModule1.Setup(x => x.Name).Returns("Module 1");
        _mockModule1.Setup(x => x.Icon).Returns("🔧");
        _mockModule1.Setup(x => x.Description).Returns("Test Module 1");
        _mockModule1.Setup(x => x.ViewModelType).Returns(typeof(string));

        _mockModule2 = new Mock<IToolModule>();
        _mockModule2.Setup(x => x.Id).Returns("module2");
        _mockModule2.Setup(x => x.Name).Returns("Module 2");
        _mockModule2.Setup(x => x.Icon).Returns("🔨");
        _mockModule2.Setup(x => x.Description).Returns("Test Module 2");
        _mockModule2.Setup(x => x.ViewModelType).Returns(typeof(int));

        _mockToolRegistry.Setup(x => x.GetTool("module1")).Returns(_mockModule1.Object);
        _mockToolRegistry.Setup(x => x.GetTool("module2")).Returns(_mockModule2.Object);
    }

    [Fact]
    public void Constructor_ShouldCreateService()
    {
        // Act
        var service = new NavigationService(_mockServiceProvider.Object, _mockToolRegistry.Object);

        // Assert
        service.Should().NotBeNull();
        service.CurrentTool.Should().BeNull();
        service.CurrentSubToolId.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullServiceProvider_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new NavigationService(null!, _mockToolRegistry.Object));
    }

    [Fact]
    public void Constructor_WithNullToolRegistry_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new NavigationService(_mockServiceProvider.Object, null!));
    }

    [Fact]
    public void NavigateTo_WithValidModuleId_ShouldNavigateSuccessfully()
    {
        // Arrange
        var service = new NavigationService(_mockServiceProvider.Object, _mockToolRegistry.Object);
        IToolModule? capturedTool = null;
        string? capturedSubToolId = null;
        service.NavigationChanged += (tool, subToolId) =>
        {
            capturedTool = tool;
            capturedSubToolId = subToolId;
        };

        // Act
        service.NavigateTo("module1");

        // Assert
        capturedTool.Should().Be(_mockModule1.Object);
        capturedSubToolId.Should().BeNull();
        service.CurrentTool.Should().Be(_mockModule1.Object);
        service.CurrentSubToolId.Should().BeNull();
        _mockToolRegistry.VerifySet(x => x.ActiveTool = _mockModule1.Object, Times.Once);
    }

    [Fact]
    public void NavigateTo_WithInvalidModuleId_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var service = new NavigationService(_mockServiceProvider.Object, _mockToolRegistry.Object);
        _mockToolRegistry.Setup(x => x.GetTool("invalid")).Returns((IToolModule?)null);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => service.NavigateTo("invalid"));
    }

    [Fact]
    public void NavigateTo_WithModuleObject_ShouldNavigateSuccessfully()
    {
        // Arrange
        var service = new NavigationService(_mockServiceProvider.Object, _mockToolRegistry.Object);
        var module = _mockModule1.Object;
        IToolModule? capturedTool = null;
        string? capturedSubToolId = null;
        service.NavigationChanged += (tool, subToolId) =>
        {
            capturedTool = tool;
            capturedSubToolId = subToolId;
        };

        // Act
        service.NavigateTo(module);

        // Assert
        capturedTool.Should().Be(module);
        capturedSubToolId.Should().BeNull();
        service.CurrentTool.Should().Be(module);
    }

    [Fact]
    public void NavigateTo_WithSubToolId_ShouldNavigateSuccessfully()
    {
        // Arrange
        var service = new NavigationService(_mockServiceProvider.Object, _mockToolRegistry.Object);
        IToolModule? capturedTool = null;
        string? capturedSubToolId = null;
        service.NavigationChanged += (tool, subToolId) =>
        {
            capturedTool = tool;
            capturedSubToolId = subToolId;
        };

        // Act
        service.NavigateTo("module1:subtool1");

        // Assert
        capturedTool.Should().Be(_mockModule1.Object);
        capturedSubToolId.Should().Be("subtool1");
        service.CurrentTool.Should().Be(_mockModule1.Object);
        service.CurrentSubToolId.Should().Be("subtool1");
    }

    [Fact]
    public void NavigateTo_WithNullModuleObject_ShouldThrowArgumentNullException()
    {
        // Arrange
        var service = new NavigationService(_mockServiceProvider.Object, _mockToolRegistry.Object);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => service.NavigateTo((IToolModule)null!));
    }

    [Fact]
    public void NavigateBack_WithEmptyHistory_ShouldDoNothing()
    {
        // Arrange
        var service = new NavigationService(_mockServiceProvider.Object, _mockToolRegistry.Object);
        int eventCallCount = 0;
        service.NavigationChanged += (tool, subToolId) => eventCallCount++;

        // Act
        service.NavigateBack();

        // Assert
        eventCallCount.Should().Be(0);
    }

    [Fact]
    public void NavigateBack_WithHistory_ShouldNavigateToPreviousTool()
    {
        // Arrange
        var service = new NavigationService(_mockServiceProvider.Object, _mockToolRegistry.Object);
        service.NavigateTo("module1");
        service.NavigateTo("module2");

        IToolModule? capturedTool = null;
        string? capturedSubToolId = null;
        service.NavigationChanged += (tool, subToolId) =>
        {
            capturedTool = tool;
            capturedSubToolId = subToolId;
        };

        // Act
        service.NavigateBack();

        // Assert
        capturedTool.Should().Be(_mockModule1.Object);
        service.CurrentTool.Should().Be(_mockModule1.Object);
    }

    [Fact]
    public void NavigateTo_ShouldMaintainNavigationHistory()
    {
        // Arrange
        var service = new NavigationService(_mockServiceProvider.Object, _mockToolRegistry.Object);

        // Act
        service.NavigateTo("module1");
        service.NavigateTo("module2");

        // Assert
        service.CurrentTool.Should().Be(_mockModule2.Object);

        // Act - Navigate back
        service.NavigateBack();

        // Assert
        service.CurrentTool.Should().Be(_mockModule1.Object);
    }

    [Fact]
    public void NavigationChanged_Event_ShouldProvideCorrectParameters()
    {
        // Arrange
        var service = new NavigationService(_mockServiceProvider.Object, _mockToolRegistry.Object);
        IToolModule? capturedTool = null;
        string? capturedSubToolId = null;
        service.NavigationChanged += (tool, subToolId) =>
        {
            capturedTool = tool;
            capturedSubToolId = subToolId;
        };

        // Act
        service.NavigateTo("module1:subtool1");

        // Assert
        capturedTool.Should().Be(_mockModule1.Object);
        capturedSubToolId.Should().Be("subtool1");
    }
}
