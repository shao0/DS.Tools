using Xunit;
using FluentAssertions;
using Moq;
using DS.Tools.Core.Interfaces;
using DS.Tools.Core.Models;

namespace DS.Tools.Tests.UnitTests;

/// <summary>
/// ToolViewModelBase 单元测试
/// 覆盖剪贴板复制（成功/失败/空文本）、状态消息自动清除与取消语义
/// </summary>
public sealed class ToolViewModelBaseTests
{
    /// <summary>
    /// 测试用子类：暴露基类 protected 成员
    /// </summary>
    private sealed class TestViewModel : ToolViewModelBase
    {
        public Task CopyTextAsync(IClipboardService clipboard, string text)
            => CopyToClipboardAsync(clipboard, text);

        public void SetStatus(string message) => ShowStatus(message);
    }

    [Fact]
    public async Task CopyToClipboardAsync_Success_SetsStatusMessage()
    {
        // Arrange
        var clipboard = new Mock<IClipboardService>();
        clipboard.Setup(c => c.SetTextAsync("text")).Returns(Task.CompletedTask);
        var vm = new TestViewModel();

        // Act
        await vm.CopyTextAsync(clipboard.Object, "text");

        // Assert
        vm.StatusMessage.Should().Be("✓ 已复制到剪贴板");
        vm.HasErrors.Should().BeFalse();
    }

    [Fact]
    public async Task CopyToClipboardAsync_EmptyText_DoesNotTouchClipboard()
    {
        // Arrange
        var clipboard = new Mock<IClipboardService>();
        var vm = new TestViewModel();

        // Act
        await vm.CopyTextAsync(clipboard.Object, "");

        // Assert
        clipboard.Verify(c => c.SetTextAsync(It.IsAny<string>()), Times.Never);
        vm.StatusMessage.Should().BeEmpty();
    }

    [Fact]
    public async Task CopyToClipboardAsync_Failure_ShowsError()
    {
        // Arrange
        var clipboard = new Mock<IClipboardService>();
        clipboard.Setup(c => c.SetTextAsync(It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("boom"));
        var vm = new TestViewModel();

        // Act
        await vm.CopyTextAsync(clipboard.Object, "text");

        // Assert
        vm.HasErrors.Should().BeTrue();
        vm.ErrorMessage.Should().Contain("复制失败");
        vm.StatusMessage.Should().BeEmpty();
    }

    [Fact]
    public async Task CopyToClipboardAsync_SuccessMessage_AutoClearsAfterDelay()
    {
        // Arrange
        var clipboard = new Mock<IClipboardService>();
        clipboard.Setup(c => c.SetTextAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        var vm = new TestViewModel();

        // Act
        await vm.CopyTextAsync(clipboard.Object, "text");
        await Task.Delay(2500); // 2 秒清除窗口

        // Assert
        vm.StatusMessage.Should().BeEmpty();
    }

    [Fact]
    public async Task ShowStatus_NewMessage_DoesNotGetClearedByPreviousTimer()
    {
        // Arrange
        var vm = new TestViewModel();

        // Act
        vm.SetStatus("first");
        await Task.Delay(2100); // 第一次的清除计时已到
        vm.StatusMessage.Should().BeEmpty(); // 旧消息已清除

        vm.SetStatus("second");
        await Task.Delay(500);
        vm.StatusMessage.Should().Be("second"); // 旧计时未误清新消息

        await Task.Delay(2000);
        vm.StatusMessage.Should().BeEmpty(); // 新计时正常清除
    }
}
