using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using DS.Tools.Core.Interfaces;
using DS.Tools.Module.Text.Services;
using DS.Tools.Module.Text.ViewModels;

namespace DS.Tools.Tests.UnitTests;

/// <summary>
/// JsonFormatterViewModel 单元测试：格式化/压缩/验证/错误/清空/复制与输入变化语义
/// </summary>
public sealed class JsonFormatterViewModelTests
{
    private static JsonFormatterViewModel CreateViewModel(IClipboardService? clipboard = null)
        => new(
            new JsonFormatterService(),
            clipboard ?? Mock.Of<IClipboardService>(),
            NullLogger<JsonFormatterViewModel>.Instance);

    [Fact]
    public async Task Format_ValidJson_SetsOutputAndStatus()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.InputJson = """{"a":1}""";

        // Act
        await vm.FormatCommand.ExecuteAsync(null);

        // Assert
        vm.HasErrors.Should().BeFalse();
        vm.HasOutput.Should().BeTrue();
        vm.OutputJson.Should().Contain("\n");
        vm.OutputJson.Should().Contain("\"a\": 1");
        vm.StatusMessage.Should().Contain("格式化成功");
        vm.IsProcessing.Should().BeFalse();
    }

    [Fact]
    public async Task Format_InvalidJson_ShowsErrorAndClearsOutput()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.InputJson = "{invalid";

        // Act
        await vm.FormatCommand.ExecuteAsync(null);

        // Assert
        vm.HasErrors.Should().BeTrue();
        vm.ErrorMessage.Should().NotBeNull();
        vm.HasOutput.Should().BeFalse();
        vm.OutputJson.Should().BeEmpty();
    }

    [Fact]
    public async Task Compress_ValidJson_ProducesCompactOutput()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.InputJson = "{\n  \"a\": 1\n}";

        // Act
        await vm.CompressCommand.ExecuteAsync(null);

        // Assert
        vm.OutputJson.Should().Be("""{"a":1}""");
        vm.StatusMessage.Should().Contain("压缩成功");
        vm.HasErrors.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ValidJson_StatusContainsDepth()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.InputJson = """{"a":{"b":1}}""";

        // Act
        await vm.ValidateCommand.ExecuteAsync(null);

        // Assert（验证操作不输出格式化 JSON，仅状态消息；HasOutput 由 ShowSuccess 统一置位）
        vm.HasErrors.Should().BeFalse();
        vm.OutputJson.Should().BeEmpty();
        vm.StatusMessage.Should().Contain("JSON 格式有效");
        vm.StatusMessage.Should().Contain("深度");
    }

    [Fact]
    public async Task Validate_InvalidJson_ShowsError()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.InputJson = "[1,2";

        // Act
        await vm.ValidateCommand.ExecuteAsync(null);

        // Assert
        vm.HasErrors.Should().BeTrue();
        vm.StatusMessage.Should().BeEmpty();
    }

    [Fact]
    public void Commands_CanExecute_OnlyWhenInputPresentAndNotProcessing()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        vm.FormatCommand.CanExecute(null).Should().BeFalse("空输入不可执行");
        vm.CompressCommand.CanExecute(null).Should().BeFalse();
        vm.ValidateCommand.CanExecute(null).Should().BeFalse();

        vm.InputJson = """{"a":1}""";
        vm.FormatCommand.CanExecute(null).Should().BeTrue();
        vm.CompressCommand.CanExecute(null).Should().BeTrue();
        vm.ValidateCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void InputChanged_ClearsOutputAndError()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.InputJson = """{"a":1}""";
        vm.OutputJson = "stale";
        vm.HasOutput = true;

        // Act（输入变化触发 partial OnInputJsonChanged）
        vm.InputJson = """{"b":2}""";

        // Assert
        vm.OutputJson.Should().BeEmpty();
        vm.HasOutput.Should().BeFalse();
        vm.HasErrors.Should().BeFalse();
    }

    [Fact]
    public async Task Clear_ResetsEverything()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.InputJson = """{"a":1}""";
        await vm.FormatCommand.ExecuteAsync(null);

        // Act
        vm.ClearCommand.Execute(null);

        // Assert
        vm.InputJson.Should().BeEmpty();
        vm.OutputJson.Should().BeEmpty();
        vm.HasOutput.Should().BeFalse();
        vm.HasErrors.Should().BeFalse();
        vm.StatusMessage.Should().BeEmpty();
    }

    [Fact]
    public async Task CopyOutput_WritesOutputToClipboard()
    {
        // Arrange
        var clipboard = new Mock<IClipboardService>();
        var vm = CreateViewModel(clipboard.Object);
        vm.InputJson = """{"a":1}""";
        await vm.FormatCommand.ExecuteAsync(null);

        // Act
        await vm.CopyOutputCommand.ExecuteAsync(null);

        // Assert
        clipboard.Verify(c => c.SetTextAsync(vm.OutputJson), Times.Once);
        vm.StatusMessage.Should().Contain("已复制");
    }

    [Fact]
    public void CopyOutput_CanExecute_OnlyWhenOutputPresent()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        vm.CopyOutputCommand.CanExecute(null).Should().BeFalse();
        vm.OutputJson = "{}";
        vm.CopyOutputCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task Format_WhileProcessing_IgnoresSecondCall()
    {
        // 防重入：处理中再次执行直接返回（AsyncRelayCommand 自带防重入，双保险）
        // Arrange
        var vm = CreateViewModel();
        vm.InputJson = """{"a":1}""";

        // Act
        var first = vm.FormatCommand.ExecuteAsync(null);
        var second = vm.FormatCommand.ExecuteAsync(null);

        // Assert
        await Task.WhenAll(first, second);
        vm.HasErrors.Should().BeFalse();
    }
}
