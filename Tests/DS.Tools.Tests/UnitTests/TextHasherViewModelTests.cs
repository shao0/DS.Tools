using Xunit;
using FluentAssertions;
using Moq;
using DS.Tools.Core.Interfaces;
using DS.Tools.Module.Text.ViewModels;

namespace DS.Tools.Tests.UnitTests;

/// <summary>
/// TextHasherViewModel 单元测试：SHA-256/MD5 已知向量、空输入、清空/复制语义
/// </summary>
public sealed class TextHasherViewModelTests
{
    private static TextHasherViewModel CreateViewModel(IClipboardService? clipboard = null)
        => new(clipboard ?? Mock.Of<IClipboardService>());

    [Fact]
    public void CalculateHash_KnownVector_ProducesStandardSha256AndMd5()
    {
        // "abc" 的标准哈希向量（RFC 6234 / RFC 1321）
        // Arrange
        var vm = CreateViewModel();
        vm.InputText = "abc";

        // Act
        vm.CalculateHashCommand.Execute(null);

        // Assert
        vm.Sha256Result.Should().Be("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
        vm.Md5Result.Should().Be("900150983cd24fb0d6963f7d28e17f72");
        vm.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void CalculateHash_ChineseText_ComputesUtf8Hash()
    {
        // "你好" UTF-8 字节的 SHA-256（与 Encoding.UTF8 一致验证）
        // Arrange
        var vm = CreateViewModel();
        vm.InputText = "你好";

        // Act
        vm.CalculateHashCommand.Execute(null);

        // Assert
        var expected = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("你好")))
            .ToLowerInvariant();
        vm.Sha256Result.Should().Be(expected);
    }

    [Fact]
    public void CalculateHash_EmptyInput_ShowsError()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.CalculateHashCommand.Execute(null);

        // Assert
        vm.HasErrors.Should().BeTrue();
        vm.ErrorMessage.Should().Be("请输入要计算哈希的文本");
        vm.Sha256Result.Should().BeEmpty();
        vm.Md5Result.Should().BeEmpty();
    }

    [Fact]
    public void CalculateHash_CanExecute_OnlyWhenInputNotBlank()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        vm.CalculateHashCommand.CanExecute(null).Should().BeFalse();
        vm.InputText = "   ";
        vm.CalculateHashCommand.CanExecute(null).Should().BeFalse("空白输入不可计算");
        vm.InputText = "data";
        vm.CalculateHashCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void Clear_ResetsAllState()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.InputText = "abc";
        vm.CalculateHashCommand.Execute(null);
        vm.ClearCommand.CanExecute(null).Should().BeTrue();

        // Act
        vm.ClearCommand.Execute(null);

        // Assert
        vm.InputText.Should().BeEmpty();
        vm.Sha256Result.Should().BeEmpty();
        vm.Md5Result.Should().BeEmpty();
        vm.HasErrors.Should().BeFalse();
    }

    [Fact]
    public async Task CopyResults_WritesToClipboard()
    {
        // Arrange
        var clipboard = new Mock<IClipboardService>();
        var vm = CreateViewModel(clipboard.Object);
        vm.InputText = "abc";
        vm.CalculateHashCommand.Execute(null);

        // Act
        await vm.CopySha256Command.ExecuteAsync(null);
        await vm.CopyMd5Command.ExecuteAsync(null);

        // Assert
        clipboard.Verify(c => c.SetTextAsync(vm.Sha256Result), Times.Once);
        clipboard.Verify(c => c.SetTextAsync(vm.Md5Result), Times.Once);
    }

    [Fact]
    public void CopyCommands_CanExecute_OnlyWhenResultsPresent()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        vm.CopySha256Command.CanExecute(null).Should().BeFalse();
        vm.CopyMd5Command.CanExecute(null).Should().BeFalse();
        vm.InputText = "x";
        vm.CalculateHashCommand.Execute(null);
        vm.CopySha256Command.CanExecute(null).Should().BeTrue();
        vm.CopyMd5Command.CanExecute(null).Should().BeTrue();
    }
}
