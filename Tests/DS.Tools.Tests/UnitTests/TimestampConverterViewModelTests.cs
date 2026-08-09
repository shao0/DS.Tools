using Xunit;
using FluentAssertions;
using DS.Tools.Module.Text.ViewModels;

namespace DS.Tools.Tests.UnitTests;

/// <summary>
/// TimestampConverterViewModel 单元测试：秒/毫秒级自动检测、往返转换、错误与清空语义
/// </summary>
public sealed class TimestampConverterViewModelTests
{
    [Fact]
    public void TimestampToDate_SecondLevel_ConvertsWithUtcOffsetDisplay()
    {
        // Arrange: 1700000000 = 2023-11-14 22:13:20 UTC。
        // DateTimeOffset.ToString() 显示其自身偏移（UnixTime 构造为 +00:00）的时间，不做本地时区转换
        var vm = new TimestampConverterViewModel();
        vm.TimestampInput = "1700000000";

        // Act
        vm.TimestampToDateCommand.Execute(null);

        // Assert
        vm.ConvertedDateTime.Should().Be("2023-11-14 22:13:20");
        vm.UtcDateTime.Should().Be("2023-11-14 22:13:20 UTC");
        vm.IsoFormat.Should().Be("2023-11-14T22:13:20.0000000+00:00");
        vm.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void TimestampToDate_MillisecondLevel_AutoDetectedByLength()
    {
        // 13 位以上为毫秒级：1700000000000 毫秒 = 1700000000 秒
        // Arrange
        var vm = new TimestampConverterViewModel();
        vm.TimestampInput = "1700000000000";

        // Act
        vm.TimestampToDateCommand.Execute(null);

        // Assert
        vm.UtcDateTime.Should().Be("2023-11-14 22:13:20 UTC");
    }

    [Fact]
    public void TimestampToDate_UnixEpoch_ConvertsTo1970()
    {
        // Arrange
        var vm = new TimestampConverterViewModel();
        vm.TimestampInput = "0";

        // Act
        vm.TimestampToDateCommand.Execute(null);

        // Assert
        vm.UtcDateTime.Should().Be("1970-01-01 00:00:00 UTC");
        vm.HasErrors.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TimestampToDate_EmptyInput_ShowsPrompt(string input)
    {
        // Arrange
        var vm = new TimestampConverterViewModel();
        vm.TimestampInput = input;

        // Act
        vm.TimestampToDateCommand.Execute(null);

        // Assert
        vm.HasErrors.Should().BeTrue();
        vm.ErrorMessage.Should().Be("请输入时间戳");
        vm.ConvertedDateTime.Should().BeEmpty();
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12.5")]
    public void TimestampToDate_NonNumericInput_ShowsFormatError(string input)
    {
        // Arrange
        var vm = new TimestampConverterViewModel();
        vm.TimestampInput = input;

        // Act
        vm.TimestampToDateCommand.Execute(null);

        // Assert
        vm.HasErrors.Should().BeTrue();
        vm.ErrorMessage.Should().Be("无效的时间戳格式");
        vm.ConvertedDateTime.Should().BeEmpty();
    }

    [Fact]
    public void DateToTimestamp_ConvertsToSecondsAndMilliseconds()
    {
        // Arrange: 2023-11-15 06:13:20（本地，与 1700000000 秒对应）
        var vm = new TimestampConverterViewModel();
        var dateTime = DateTimeOffset.FromUnixTimeSeconds(1700000000).ToLocalTime();
        vm.DateInput = dateTime.ToString("yyyy-MM-dd HH:mm:ss");

        // Act
        vm.DateToTimestampCommand.Execute(null);

        // Assert
        vm.SecondsTimestamp.Should().Be("1700000000");
        vm.MillisecondsTimestamp.Should().Be("1700000000000");
        vm.HasErrors.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void DateToTimestamp_EmptyInput_ShowsPrompt(string input)
    {
        // Arrange
        var vm = new TimestampConverterViewModel();
        vm.DateInput = input;

        // Act
        vm.DateToTimestampCommand.Execute(null);

        // Assert
        vm.HasErrors.Should().BeTrue();
        vm.ErrorMessage.Should().Be("请输入日期时间");
        vm.SecondsTimestamp.Should().BeEmpty();
    }

    [Theory]
    [InlineData("not a date")]
    [InlineData("2023-13-99")]
    public void DateToTimestamp_InvalidDate_ShowsFormatError(string input)
    {
        // Arrange
        var vm = new TimestampConverterViewModel();
        vm.DateInput = input;

        // Act
        vm.DateToTimestampCommand.Execute(null);

        // Assert
        vm.HasErrors.Should().BeTrue();
        vm.ErrorMessage.Should().Contain("无效的日期格式");
        vm.SecondsTimestamp.Should().BeEmpty();
    }

    [Fact]
    public void TimestampDate_RoundTrip_PreservesInstant()
    {
        // 时间戳 → 本地日期字符串 → 时间戳 往返一致（输入解析为本地时区，往返不变）
        // Arrange
        var vm = new TimestampConverterViewModel();
        vm.TimestampInput = "1700000000";
        vm.TimestampToDateCommand.Execute(null);
        var localDate = DateTimeOffset.FromUnixTimeSeconds(1700000000).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

        // Act
        vm.DateInput = localDate;
        vm.DateToTimestampCommand.Execute(null);

        // Assert
        vm.SecondsTimestamp.Should().Be("1700000000");
    }

    [Fact]
    public void TimestampToDate_CanExecute_OnlyWhenInputPresent()
    {
        // Arrange
        var vm = new TimestampConverterViewModel();

        // Act & Assert
        vm.TimestampToDateCommand.CanExecute(null).Should().BeFalse();
        vm.TimestampInput = "1";
        vm.TimestampToDateCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void ClearTimestamp_ResetsTimestampState()
    {
        // Arrange
        var vm = new TimestampConverterViewModel();
        vm.TimestampInput = "1700000000";
        vm.TimestampToDateCommand.Execute(null);

        // Act
        vm.ClearTimestampCommand.Execute(null);

        // Assert
        vm.TimestampInput.Should().BeEmpty();
        vm.ConvertedDateTime.Should().BeEmpty();
        vm.UtcDateTime.Should().BeEmpty();
        vm.IsoFormat.Should().BeEmpty();
        vm.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void ClearDate_ResetsDateState()
    {
        // Arrange
        var vm = new TimestampConverterViewModel();
        vm.DateInput = "2023-11-15 06:13:20";
        vm.DateToTimestampCommand.Execute(null);

        // Act
        vm.ClearDateCommand.Execute(null);

        // Assert
        vm.DateInput.Should().BeEmpty();
        vm.SecondsTimestamp.Should().BeEmpty();
        vm.MillisecondsTimestamp.Should().BeEmpty();
    }
}
