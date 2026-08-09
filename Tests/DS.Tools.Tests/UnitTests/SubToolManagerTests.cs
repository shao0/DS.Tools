using Xunit;
using FluentAssertions;
using DS.Tools.Module.Base;

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
        var subTool = new SubToolInfo("test1", "Test Tool 1", "🔧", typeof(string));

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
        var subTool1 = new SubToolInfo("test1", "Test Tool 1", "🔧", typeof(string));
        var subTool2 = new SubToolInfo("test1", "Test Tool 2", "🔨", typeof(int));
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
            new SubToolInfo("test1", "Test Tool 1", "🔧", typeof(string)),
            new SubToolInfo("test2", "Test Tool 2", "🔨", typeof(int)),
            new SubToolInfo("test3", "Test Tool 3", "⚙️", typeof(double))
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
        var subTool = new SubToolInfo("test1", "Test Tool 1", "🔧", typeof(string));
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
    public void GetSubToolViewModelType_WithExistingId_ShouldReturnCorrectType()
    {
        // Arrange
        var manager = new SubToolManager("test-module");
        var subTool = new SubToolInfo("test1", "Test Tool 1", "🔧", typeof(string));
        manager.AddSubTool(subTool);

        // Act
        var result = manager.GetSubToolViewModelType("test1");

        // Assert
        result.Should().Be(typeof(string));
    }

    [Fact]
    public void GetSubToolViewModelType_WithNonExistingId_ShouldReturnNull()
    {
        // Arrange
        var manager = new SubToolManager("test-module");

        // Act
        var result = manager.GetSubToolViewModelType("nonexistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ContainsSubTool_WithExistingId_ShouldReturnTrue()
    {
        // Arrange
        var manager = new SubToolManager("test-module");
        var subTool = new SubToolInfo("test1", "Test Tool 1", "🔧", typeof(string));
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
            new SubToolInfo("test1", "Test Tool 1", "🔧", typeof(string)),
            new SubToolInfo("test2", "Test Tool 2", "🔨", typeof(int))
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
        var subTool = new SubToolInfo("test1", "Test Tool 1", "🔧", typeof(string));
        var moduleId = "test-module";

        // Act
        var result = subTool.GetFullNavigationId(moduleId);

        // Assert
        result.Should().Be("test-module:test1");
    }
}
