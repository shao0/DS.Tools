using Xunit;
using FluentAssertions;
using Moq;
using DS.Tools.Core.Interfaces;
using DS.Tools.Module.Text.ViewModels;

namespace DS.Tools.Tests.UnitTests;

/// <summary>
/// Base64ViewModel 单元测试：编码/解码/清空/复制/CanExecute 语义
/// </summary>
public sealed class Base64ViewModelTests
{
    private static Base64ViewModel CreateViewModel(IClipboardService? clipboard = null)
        => new(clipboard ?? Mock.Of<IClipboardService>());

    [Fact]
    public void Encode_Utf8Text_ProducesCorrectBase64()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.InputText = "你好";

        // Act
        vm.EncodeCommand.Execute(null);

        // Assert
        vm.EncodedResult.Should().Be(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("你好")));
        vm.HasErrors.Should().BeFalse();
        vm.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Encode_EmptyInput_ShowsErrorAndNoResult()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.EncodeCommand.Execute(null);

        // Assert
        vm.HasErrors.Should().BeTrue();
        vm.ErrorMessage.Should().Be("请输入要编码的文本");
        vm.EncodedResult.Should().BeEmpty();
    }

    [Fact]
    public void Encode_CanExecute_OnlyWhenInputNotEmpty()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        vm.EncodeCommand.CanExecute(null).Should().BeFalse("空输入不可编码");
        vm.InputText = "abc";
        vm.EncodeCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void Decode_ValidBase64_ProducesOriginalText()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.InputText = "5L2g5aW9";

        // Act
        vm.DecodeCommand.Execute(null);

        // Assert
        vm.DecodedResult.Should().Be("你好");
        vm.HasErrors.Should().BeFalse();
    }

    [Theory]
    [InlineData("!!!not-base64!!!")]
    [InlineData("aaaa=bbbb")]
    [InlineData("###")]
    public void Decode_InvalidBase64_ShowsError(string invalid)
    {
        // Arrange
        var vm = CreateViewModel();
        vm.InputText = invalid;

        // Act
        vm.DecodeCommand.Execute(null);

        // Assert
        vm.HasErrors.Should().BeTrue();
        vm.ErrorMessage.Should().Be("无效的 Base64 字符串");
        vm.DecodedResult.Should().BeEmpty();
    }

    [Fact]
    public void Decode_EmptyInput_ShowsError()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.DecodeCommand.Execute(null);

        // Assert
        vm.HasErrors.Should().BeTrue();
        vm.ErrorMessage.Should().Be("请输入 Base64 字符串");
    }

    [Fact]
    public void EncodeDecode_RoundTrip_PreservesText()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.InputText = "DS.Tools - 跨平台工具集 !@#$%^&*()_+";

        // Act
        vm.EncodeCommand.Execute(null);
        var encoded = vm.EncodedResult;
        vm.InputText = encoded;
        vm.DecodeCommand.Execute(null);

        // Assert
        vm.DecodedResult.Should().Be("DS.Tools - 跨平台工具集 !@#$%^&*()_+");
    }

    [Fact]
    public void Clear_ResetsAllState()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.InputText = "hello";
        vm.EncodeCommand.Execute(null);

        // Act
        vm.ClearCommand.Execute(null);

        // Assert
        vm.InputText.Should().BeEmpty();
        vm.EncodedResult.Should().BeEmpty();
        vm.DecodedResult.Should().BeEmpty();
        vm.HasErrors.Should().BeFalse();
        vm.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Clear_CanExecute_WhenAnyStatePresent()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        vm.ClearCommand.CanExecute(null).Should().BeFalse("全空状态不可清空");
        vm.InputText = "x";
        vm.ClearCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task CopyEncoded_WritesResultToClipboard()
    {
        // Arrange
        var clipboard = new Mock<IClipboardService>();
        var vm = CreateViewModel(clipboard.Object);
        vm.InputText = "abc";
        vm.EncodeCommand.Execute(null);

        // Act
        await vm.CopyEncodedCommand.ExecuteAsync(null);

        // Assert
        clipboard.Verify(c => c.SetTextAsync(vm.EncodedResult), Times.Once);
        vm.StatusMessage.Should().Contain("已复制");
    }

    [Fact]
    public void CopyEncoded_CanExecute_OnlyWhenResultExists()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        vm.CopyEncodedCommand.CanExecute(null).Should().BeFalse();
        vm.CopyDecodedCommand.CanExecute(null).Should().BeFalse();
        vm.InputText = "aGk=";
        vm.DecodeCommand.Execute(null);
        vm.CopyDecodedCommand.CanExecute(null).Should().BeTrue();
    }
}
