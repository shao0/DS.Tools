using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Data.Converters;

namespace DS.Tools.Module.Git.Converters;

/// <summary>
/// 提交消息显示转换：压缩连续换行为单个换行（<c>\n{2,}</c> → <c>\n</c>）。
/// 仅作用于显示层（TextBlock 绑定），复制走 VM 原始 Message 不受影响。
/// 动机：Avalonia 12.1.x 的 <see cref="Avalonia.Media.TextFormatting.TextLayout"/> 在
/// TextWrapping=Wrap 且文本含空段落（如提交正文的段落间空行）时，断行循环不终止，
/// 无限分配行对象导致内存爆炸挂起。压缩空行后不再产生空段落，规避该框架 bug。
/// </summary>
public sealed class GitLogMessageConverter : IValueConverter
{
    private static readonly Regex s_multiNewline = new("\n{2,}", RegexOptions.Compiled);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string text && s_multiNewline.IsMatch(text)
            ? s_multiNewline.Replace(text, "\n")
            : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
