using Xunit;
using FluentAssertions;
using DS.Tools.Module.Text.Models;
using DS.Tools.Module.Text.Services;

namespace DS.Tools.Tests.UnitTests;

/// <summary>
/// JsonFormatterService 单元测试
/// 覆盖格式化、压缩、验证与深度计算的核心行为
/// </summary>
public sealed class JsonFormatterServiceTests
{
    private readonly JsonFormatterService _service = new();

    [Fact]
    public void Format_ValidJson_ShouldReturnIndentedOutput()
    {
        // Arrange
        const string json = """{"name":"DS.Tools","version":1}""";

        // Act
        var result = _service.Format(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.FormattedJson.Should().Contain("\n");
        result.OperationType.Should().Be(JsonFormatterOperationType.Format);
        result.OriginalLength.Should().Be(json.Length);
        result.ProcessedLength.Should().BeGreaterThan(json.Length);
    }

    [Fact]
    public void Format_EmptyInput_ShouldReturnFailure()
    {
        // Act
        var result = _service.Format("   ");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("不能为空");
    }

    [Fact]
    public void Format_InvalidJson_ShouldReturnFailure()
    {
        // Act
        var result = _service.Format("{\"name\":}");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("JSON 格式错误");
    }

    [Fact]
    public void Compress_ValidJson_ShouldRemoveAllWhitespace()
    {
        // Arrange
        const string json = """
            {
                "name": "DS.Tools",
                "version": 1
            }
            """;

        // Act
        var result = _service.Compress(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.FormattedJson.Should().Be("""{"name":"DS.Tools","version":1}""");
        result.OperationType.Should().Be(JsonFormatterOperationType.Compress);
    }

    [Fact]
    public void Compress_InvalidJson_ShouldReturnFailure()
    {
        // Act
        var result = _service.Compress("[1,2,");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.OperationType.Should().Be(JsonFormatterOperationType.Compress);
    }

    [Fact]
    public void Validate_ValidJson_ShouldReturnSuccess()
    {
        // Act
        var result = _service.Validate("""{"a":1}""");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.OperationType.Should().Be(JsonFormatterOperationType.Validate);
        result.FormattedJson.Should().BeEmpty(); // 验证操作无输出
    }

    [Fact]
    public void Validate_InvalidJson_ShouldReturnFailure()
    {
        // Act
        var result = _service.Validate("not-json");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.OperationType.Should().Be(JsonFormatterOperationType.Validate);
    }

    [Fact]
    public void Validate_NestedJson_ShouldReportDepth()
    {
        // Arrange
        const string json = """{"a":{"b":{"c":1}}}""";

        // Act
        var result = _service.Validate(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.JsonDepth.Should().Be(3);
    }

    [Fact]
    public void Format_WithNumberVariants_ShouldPreserveValues()
    {
        // Arrange
        const string json = """{"int":42,"double":3.14,"big":1.5E+30}""";

        // Act
        var formatResult = _service.Format(json);
        var roundTrip = _service.Compress(formatResult.FormattedJson!);

        // Assert
        roundTrip.IsSuccess.Should().BeTrue();
        roundTrip.FormattedJson.Should().Contain("42");
        roundTrip.FormattedJson.Should().Contain("3.14");
    }
}
