using Xunit;
using FluentAssertions;
using Moq;
using DS.Tools.Core.Interfaces;
using DS.Tools.Module.Text.ViewModels;

namespace DS.Tools.Tests.UnitTests;

/// <summary>
/// PasswordGeneratorViewModel 单元测试：字符集组合/长度/错误/复制语义
/// </summary>
public sealed class PasswordGeneratorViewModelTests
{
    private static PasswordGeneratorViewModel CreateViewModel(IClipboardService? clipboard = null)
        => new(clipboard ?? Mock.Of<IClipboardService>());

    private const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Lowercase = "abcdefghijklmnopqrstuvwxyz";
    private const string Numbers = "0123456789";
    private const string Symbols = "!@#$%^&*()_+-=[]{}|;:,.<>?";

    [Fact]
    public void Generate_DefaultOptions_Produces16CharPasswordWithUppercaseLowercaseNumbers()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.GenerateCommand.Execute(null);

        // Assert
        vm.GeneratedPassword.Should().HaveLength(16);
        Uppercase.Any(vm.GeneratedPassword.Contains).Should().BeTrue("密码应含大写字母");
        Lowercase.Any(vm.GeneratedPassword.Contains).Should().BeTrue("密码应含小写字母");
        Numbers.Any(vm.GeneratedPassword.Contains).Should().BeTrue("密码应含数字");
        vm.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void Generate_PasswordLengthChanged_RespectsLength()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.PasswordLength = 32;

        // Act
        vm.GenerateCommand.Execute(null);

        // Assert
        vm.GeneratedPassword.Should().HaveLength(32);
    }

    [Fact]
    public void Generate_OnlyUppercase_AllCharactersUppercase()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.UseUppercase = true;
        vm.UseLowercase = false;
        vm.UseNumbers = false;
        vm.UseSymbols = false;

        // Act
        vm.GenerateCommand.Execute(null);

        // Assert
        vm.GeneratedPassword.Should().NotBeEmpty();
        vm.GeneratedPassword.All(Uppercase.Contains).Should().BeTrue("仅大写字符集时密码只含大写字母");
    }

    [Fact]
    public void Generate_OnlySymbols_AllCharactersSymbols()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.UseUppercase = false;
        vm.UseLowercase = false;
        vm.UseNumbers = false;
        vm.UseSymbols = true;

        // Act
        vm.GenerateCommand.Execute(null);

        // Assert
        vm.GeneratedPassword.Should().NotBeEmpty();
        vm.GeneratedPassword.All(Symbols.Contains).Should().BeTrue("仅符号字符集时密码只含符号");
    }

    [Fact]
    public void Generate_NoCharacterSet_ShowsErrorAndEmptyPassword()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.UseUppercase = false;
        vm.UseLowercase = false;
        vm.UseNumbers = false;
        vm.UseSymbols = false;

        // Act
        vm.GenerateCommand.Execute(null);

        // Assert
        vm.HasErrors.Should().BeTrue();
        vm.ErrorMessage.Should().Be("请至少选择一种字符类型");
        vm.GeneratedPassword.Should().BeEmpty();
    }

    [Fact]
    public void Generate_MultipleTimes_ProducesVaryingResults()
    {
        // 密码学随机源：连续生成两次几乎必然不同（验证随机性非固定输出）
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.GenerateCommand.Execute(null);
        var first = vm.GeneratedPassword;
        vm.GenerateCommand.Execute(null);
        var second = vm.GeneratedPassword;

        // Assert
        first.Should().NotBe(second);
    }

    [Fact]
    public void PasswordLengthDisplay_ReflectsLength()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.PasswordLength = 24;

        // Assert
        vm.PasswordLengthDisplay.Should().Be("24");
    }

    [Fact]
    public async Task Copy_GeneratedPassword_WritesToClipboard()
    {
        // Arrange
        var clipboard = new Mock<IClipboardService>();
        var vm = CreateViewModel(clipboard.Object);
        vm.GenerateCommand.Execute(null);

        // Act
        await vm.CopyCommand.ExecuteAsync(null);

        // Assert
        clipboard.Verify(c => c.SetTextAsync(vm.GeneratedPassword), Times.Once);
        vm.StatusMessage.Should().Contain("已复制");
    }

    [Fact]
    public void Copy_CanExecute_OnlyWhenPasswordGenerated()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        vm.CopyCommand.CanExecute(null).Should().BeFalse();
        vm.GenerateCommand.Execute(null);
        vm.CopyCommand.CanExecute(null).Should().BeTrue();
    }
}
