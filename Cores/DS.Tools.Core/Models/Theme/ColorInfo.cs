namespace DS.Tools.Core.Models.Theme;

/// <summary>
/// 颜色信息
/// </summary>
public record class ColorInfo
{
    /// <summary>
    /// 颜色名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// HEX值
    /// </summary>
    public required string Hex { get; init; }

    /// <summary>
    /// RGB值
    /// </summary>
    public required string Rgb { get; init; }

    /// <summary>
    /// HSL值
    /// </summary>
    public required string Hsl { get; init; }
}