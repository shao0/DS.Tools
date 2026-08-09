using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using DS.Tools.Module.Base.Interfaces;
using DS.Tools.Module.Base.Services;

namespace DS.Tools.Tests.UnitTests;

/// <summary>
/// ToolRegistry 单元测试
/// 测试工具注册表的核心功能
/// </summary>
public sealed class ToolRegistryTests
{
    private readonly Mock<IToolModule> _mockModule1;
    private readonly Mock<IToolModule> _mockModule2;

    /// <summary>
    /// 创建注册表（统一目录为必需构造依赖；本组测试用 mock 模块，空目录即可）
    /// </summary>
    private static ToolRegistry CreateRegistry() => new(new ToolCatalog([], [], Mock.Of<IServiceProvider>()));

    public ToolRegistryTests()
    {
        _mockModule1 = new Mock<IToolModule>();
        _mockModule1.Setup(x => x.Id).Returns("module1");
        _mockModule1.Setup(x => x.Name).Returns("Module 1");
        _mockModule1.Setup(x => x.Icon).Returns("🔧");
        _mockModule1.Setup(x => x.Description).Returns("Test Module 1");

        _mockModule2 = new Mock<IToolModule>();
        _mockModule2.Setup(x => x.Id).Returns("module2");
        _mockModule2.Setup(x => x.Name).Returns("Module 2");
        _mockModule2.Setup(x => x.Icon).Returns("🔨");
        _mockModule2.Setup(x => x.Description).Returns("Test Module 2");
    }

    [Fact]
    public void Constructor_ShouldCreateEmptyRegistry()
    {
        // Act
        var registry = CreateRegistry();

        // Assert
        registry.Tools.Should().BeEmpty();
    }

    [Fact]
    public void Register_WithValidModule_ShouldAddSuccessfully()
    {
        // Arrange
        var registry = CreateRegistry();
        var module = _mockModule1.Object;

        // Act
        registry.Register(module);

        // Assert
        registry.Tools.Should().ContainSingle();
        registry.Tools.Should().Contain(module);
    }

    [Fact]
    public void Register_WithDuplicateId_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var registry = CreateRegistry();
        var module1 = _mockModule1.Object;
        var module2 = _mockModule1.Object; // Same ID
        registry.Register(module1);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => registry.Register(module2));
    }

    [Fact]
    public void Register_WithNullModule_ShouldThrowArgumentNullException()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
    }

    [Fact]
    public void GetTool_WithExistingId_ShouldReturnCorrectModule()
    {
        // Arrange
        var registry = CreateRegistry();
        var module = _mockModule1.Object;
        registry.Register(module);

        // Act
        var result = registry.GetTool("module1");

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(module);
    }

    [Fact]
    public void GetTool_WithNonExistingId_ShouldReturnNull()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.GetTool("nonexistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Tools_ShouldReturnReadOnlyList()
    {
        // Arrange
        var registry = CreateRegistry();
        var module1 = _mockModule1.Object;
        var module2 = _mockModule2.Object;
        registry.Register(module1);
        registry.Register(module2);

        // Act
        var tools = registry.Tools;

        // Assert
        tools.Should().HaveCount(2);
        tools.Should().BeAssignableTo<System.Collections.Generic.IReadOnlyList<IToolModule>>();
    }

    [Fact]
    public void Register_MultipleModules_ShouldMaintainOrder()
    {
        // Arrange
        var registry = CreateRegistry();
        var module1 = _mockModule1.Object;
        var module2 = _mockModule2.Object;

        // Act
        registry.Register(module1);
        registry.Register(module2);

        // Assert
        registry.Tools.Should().HaveCount(2);
        registry.Tools[0].Should().Be(module1);
        registry.Tools[1].Should().Be(module2);
    }
}
