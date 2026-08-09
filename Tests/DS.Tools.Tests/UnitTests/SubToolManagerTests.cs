using Xunit;
using FluentAssertions;
using DS.Tools.Module.Base;
using DS.Tools.Core.Models;

namespace DS.Tools.Tests.UnitTests;

/// <summary>
/// SubToolManager 单元测试
/// 测试子工具管理器的核心功能
/// </summary>
public sealed class SubToolManagerTests
{
    [Fact]
    public void Constructor_WithValidModuleId_ShouldCreateManager()
    {
        // Act
        var manager = new SubToolManager("test-module");

        // Assert
        manager.Should().NotBeNull();
        manager.SubTools.Should().BeEmpty();
        manager.Count.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithNullModuleId_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SubToolManager(null!));
    }

    [Fact]
    public void AddSubTool_WithValidSubTool_ShouldAddSuccessfully()
    {
        // Arrange
        var manager = new SubToolManager("test-module");
        var subTool = new SubToolInfo("test1", "Test Tool 1", "🔧", _ => new TestViewModel());

        // Act
        manager.AddSubTool(subTool);

        // Assert
        manager.SubTools.Should().ContainSingle();
        manager.Count.Should().Be(1);
        manager.ContainsSubTool("test1").Should().BeTrue();
    }

    [Fact]
    public void AddSubTool_WithDuplicateId_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var manager = new SubToolManager("test-module");
        var subTool1 = new SubToolInfo("test1", "Test Tool 1", "🔧", _ => new TestViewModel());
        var subTool2 = new SubToolInfo("test1", "Test Tool 2", "🔨", _ => new TestViewModel());
        manager.AddSubTool(subTool1);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => manager.AddSubTool(subTool2));
    }

    [Fact]
    public void AddSubTools_WithMultipleSubTools_ShouldAddAllSuccessfully()
    {
        // Arrange
        var manager = new SubToolManager("test-module");
        var subTools = new[]
        {
            new SubToolInfo("test1", "Test Tool 1", "🔧", _ => new TestViewModel()),
            new SubToolInfo("test2", "Test Tool 2", "🔨", _ => new TestViewModel()),
            new SubToolInfo("test3", "Test Tool 3", "⚙️", _ => new TestViewModel())
        };

        // Act
        manager.AddSubTools(subTools);

        // Assert
        manager.SubTools.Should().HaveCount(3);
        manager.Count.Should().Be(3);
    }

    [Fact]
    public void GetSubTool_WithExistingId_ShouldReturnSubTool()
    {
        // Arrange
        var manager = new SubToolManager("test-module");
        var subTool = new SubToolInfo("test1", "Test Tool 1", "🔧", _ => new TestViewModel());
        manager.AddSubTool(subTool);

        // Act
        var result = manager.GetSubTool("test1");

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(subTool);
    }

    [Fact]
    public void GetSubTool_WithNonExistingId_ShouldReturnNull()
    {
        // Arrange
        var manager = new SubToolManager("test-module");

        // Act
        var result = manager.GetSubTool("nonexistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetSubToolViewModelFactory_WithExistingId_ShouldReturnFactory()
    {
        // Arrange
        var manager = new SubToolManager("test-module");
        var subTool = new SubToolInfo("test1", "Test Tool 1", "🔧", _ => new TestViewModel());
        manager.AddSubTool(subTool);

        // Act
        var factory = manager.GetSubToolViewModelFactory("test1");

        // Assert
        factory.Should().NotBeNull();
        factory!(new TestServiceProvider()).Should().BeOfType<TestViewModel>();
    }

    [Fact]
    public void GetSubToolViewModelFactory_WithNonExistingId_ShouldReturnNull()
    {
        // Arrange
        var manager = new SubToolManager("test-module");

        // Act
        var result = manager.GetSubToolViewModelFactory("nonexistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ContainsSubTool_WithExistingId_ShouldReturnTrue()
    {
        // Arrange
        var manager = new SubToolManager("test-module");
        var subTool = new SubToolInfo("test1", "Test Tool 1", "🔧", _ => new TestViewModel());
        manager.AddSubTool(subTool);

        // Act
        var result = manager.ContainsSubTool("test1");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ContainsSubTool_WithNonExistingId_ShouldReturnFalse()
    {
        // Arrange
        var manager = new SubToolManager("test-module");

        // Act
        var result = manager.ContainsSubTool("nonexistent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Clear_ShouldRemoveAllSubTools()
    {
        // Arrange
        var manager = new SubToolManager("test-module");
        var subTools = new[]
        {
            new SubToolInfo("test1", "Test Tool 1", "🔧", _ => new TestViewModel()),
            new SubToolInfo("test2", "Test Tool 2", "🔨", _ => new TestViewModel())
        };
        manager.AddSubTools(subTools);

        // Act
        manager.Clear();

        // Assert
        manager.SubTools.Should().BeEmpty();
        manager.Count.Should().Be(0);
    }

    [Fact]
    public void SubToolInfo_GetFullNavigationId_ShouldReturnCorrectFormat()
    {
        // Arrange
        var subTool = new SubToolInfo("test1", "Test Tool 1", "🔧", _ => new TestViewModel());
        var moduleId = "test-module";

        // Act
        var result = subTool.GetFullNavigationId(moduleId);

        // Assert
        result.Should().Be("test-module:test1");
    }

    /// <summary>
    /// 测试用 ViewModel
    /// </summary>
    private sealed class TestViewModel : ViewModelBase;

    /// <summary>
    /// 测试用 IServiceProvider（工厂不接受真实容器依赖，仅传参）
    /// </summary>
    private sealed class TestServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
