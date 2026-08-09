using System.Globalization;
using Xunit;
using FluentAssertions;
using DS.Tools.Module.Git.Converters;

namespace DS.Tools.Tests.UnitTests;

/// <summary>
/// GitLogMessageConverter 单元测试：显示层空段落压缩语义
/// （规避 Avalonia 12.1.x TextWrapping=Wrap + 空段落布局死循环）
/// </summary>
public sealed class GitLogMessageConverterTests
{
    private static readonly GitLogMessageConverter s_converter = new();

    [Theory]
    [InlineData("feat: body\n\nline one\n\nline three", "feat: body\nline one\nline three")]
    [InlineData("a\n\n\nb", "a\nb")]
    [InlineData("a\n\n", "a\n")]
    [InlineData("\n\na", "\na")]
    public void Convert_ConsecutiveNewlines_CollapsesToSingle(string input, string expected)
    {
        // Act
        var result = s_converter.Convert(input, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("fix: single line")]
    [InlineData("feat: body\nline two")] // 单 \n 多行（无空段落）不压缩
    [InlineData("")]
    public void Convert_NoEmptyParagraphs_Unchanged(string input)
    {
        // Act
        var result = s_converter.Convert(input, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(input);
    }

    [Fact]
    public void Convert_NonStringValue_ReturnsAsIs()
    {
        // Act
        var result = s_converter.Convert(42, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public void Convert_Null_ReturnsNull()
    {
        // Act
        var result = s_converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupported()
    {
        // Act
        var act = () => s_converter.ConvertBack("value", typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        act.Should().Throw<NotSupportedException>();
    }
}
