using Xunit;
using FluentAssertions;
using Avalonia.Media;
using DS.Tools.Module.Text.ViewModels;

namespace DS.Tools.Tests.UnitTests;

/// <summary>
/// ColorConverterViewModel 单元测试：HEX→RGB/HSL 转换、3/6 位展开、中间态与错误语义
/// </summary>
public sealed class ColorConverterViewModelTests
{
    [Fact]
    public void Constructor_HasDefaultBlueColor()
    {
        // Arrange & Act
        var vm = new ColorConverterViewModel();

        // Assert（ColorPreview 为 Avalonia Color.ToString()：小写 #aarrggbb 形式）
        vm.HexInput.Should().Be("#3B82F6");
        vm.RgbOutput.Should().Be("rgb(59, 130, 246)");
        vm.HslOutput.Should().Be("hsl(217, 91%, 60%)");
        vm.ColorPreview.Should().Be("#ff3b82f6");
    }

    [Theory]
    [InlineData("#FF0000", "rgb(255, 0, 0)", "hsl(0, 100%, 50%)")]
    [InlineData("#00FF00", "rgb(0, 255, 0)", "hsl(120, 100%, 50%)")]
    [InlineData("#0000FF", "rgb(0, 0, 255)", "hsl(240, 100%, 50%)")]
    [InlineData("#000000", "rgb(0, 0, 0)", "hsl(0, 0%, 0%)")]
    [InlineData("#FFFFFF", "rgb(255, 255, 255)", "hsl(0, 0%, 100%)")]
    [InlineData("#808080", "rgb(128, 128, 128)", "hsl(0, 0%, 50%)")]
    [InlineData("#3B82F7", "rgb(59, 130, 247)", "hsl(217, 92%, 60%)")] // 与初始值不同的同族色，验证计算路径
    public void HexInput_ValidSixDigit_ConvertsToRgbAndHsl(string hex, string expectedRgb, string expectedHsl)
    {
        // Arrange
        var vm = new ColorConverterViewModel();

        // Act
        vm.HexInput = hex;

        // Assert
        vm.RgbOutput.Should().Be(expectedRgb);
        vm.HslOutput.Should().Be(expectedHsl);
        vm.HasErrors.Should().BeFalse();
    }

    [Theory]
    [InlineData("#F00", "rgb(255, 0, 0)")]
    [InlineData("#0F0", "rgb(0, 255, 0)")]
    [InlineData("#00F", "rgb(0, 0, 255)")]
    [InlineData("#FFF", "rgb(255, 255, 255)")]
    public void HexInput_ThreeDigit_ExpandsToSix(string hex, string expectedRgb)
    {
        // Arrange
        var vm = new ColorConverterViewModel();

        // Act
        vm.HexInput = hex;

        // Assert
        vm.RgbOutput.Should().Be(expectedRgb);
        vm.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void HexInput_WithoutHashPrefix_StillParses()
    {
        // Arrange
        var vm = new ColorConverterViewModel();

        // Act
        vm.HexInput = "FF0000";

        // Assert
        vm.RgbOutput.Should().Be("rgb(255, 0, 0)");
        vm.HasErrors.Should().BeFalse();
    }

    [Theory]
    [InlineData("#GGGGGG")] // 6 位完整但非 HEX
    [InlineData("#GGG")]    // 3 位完整但非 HEX
    public void HexInput_CompleteButInvalid_ShowsError(string hex)
    {
        // Arrange
        var vm = new ColorConverterViewModel();

        // Act
        vm.HexInput = hex;

        // Assert
        vm.HasErrors.Should().BeTrue();
        vm.ErrorMessage.Should().Be("无效的 HEX 颜色值");
    }

    [Theory]
    [InlineData("#3B8")]    // 3 位完整且有效——正常转换
    [InlineData("#3B82")]   // 中间态（4 位）
    [InlineData("#12345")]  // 中间态（5 位）
    [InlineData("#1234567")] // 中间态（7 位）
    [InlineData("#")]       // 中间态（空）
    public void HexInput_IncompleteInput_DoesNotShowError(string hex)
    {
        // 仅完整 3/6 位且无效才报错；其余为输入中间态，静默不报错
        // Arrange
        var vm = new ColorConverterViewModel();

        // Act
        vm.HexInput = hex;

        // Assert
        vm.HasErrors.Should().BeFalse();
        vm.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void HexInput_EmptyInput_NoErrorKeepsPreviousOutput()
    {
        // 空/中间态输入不报错，转换不执行（输出保留旧值，避免输入途中闪烁）
        // Arrange
        var vm = new ColorConverterViewModel();

        // Act
        vm.HexInput = "";

        // Assert
        vm.HasErrors.Should().BeFalse();
        vm.RgbOutput.Should().Be("rgb(59, 130, 246)");
    }

    [Fact]
    public void Reset_RestoresDefaultAndClearsErrors()
    {
        // Arrange
        var vm = new ColorConverterViewModel();
        vm.HexInput = "#GGGGGG";
        vm.HasErrors.Should().BeTrue();

        // Act
        vm.ResetCommand.Execute(null);

        // Assert
        vm.HexInput.Should().Be("#3B82F6");
        vm.RgbOutput.Should().Be("rgb(59, 130, 246)");
        vm.HasErrors.Should().BeFalse();
        vm.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void PreviewColor_UpdatesWithHexInput()
    {
        // Arrange
        var vm = new ColorConverterViewModel();

        // Act
        vm.HexInput = "#FF0000";

        // Assert（纯红为 Avalonia 命名色，Color.ToString() 返回名称 "Red"；非命名色返回 #rrggbb）
        vm.PreviewColor.Should().Be(Color.FromRgb(255, 0, 0));
        vm.ColorPreview.Should().Be("Red");
    }
}
