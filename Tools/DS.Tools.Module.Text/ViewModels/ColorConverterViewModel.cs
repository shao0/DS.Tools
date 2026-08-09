using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DS.Tools.Module.Base.Interfaces;


namespace DS.Tools.Module.Text.ViewModels;

/// <summary>
/// 颜色转换器 ViewModel - 在 HEX、RGB、HSL 之间转换颜色
/// AOT 兼容，使用数学计算和 Avalonia Color 结构体
/// </summary>
public sealed partial class ColorConverterViewModel : ViewModelBase, ISubTool
{
    // 子工具元数据（ISubTool 静态抽象接口实现）：经 ToolRegistration.AddSubTool<T>() 编译期读取注册
    static string ISubTool.ModuleId => TextModule.ToolIds.Module;
    static string ISubTool.Id => TextModule.ToolIds.ColorConverter;
    static string ISubTool.Name => "颜色转换";
    static string ISubTool.Icon => "🎨";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ColorPreview))]
    private string _hexInput = "#3B82F6";

    [ObservableProperty]
    private string _rgbOutput = "rgb(59, 130, 246)";

    [ObservableProperty]
    private string _hslOutput = "hsl(217, 91%, 60%)";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ColorPreview))]
    private Color _previewColor = Color.FromRgb(59, 130, 246);

    /// <summary>颜色预览字符串（HEX 形式），只读</summary>
    public string ColorPreview => PreviewColor.ToString();

    /// <summary>
    /// HEX 输入变化时自动触发转换。
    /// 中间态（如输入到 "#3B82" 的途中）不报错——仅在输入完整（3 或 6 位）且无效时显示错误。
    /// </summary>
    partial void OnHexInputChanged(string value)
    {
        // 输入完整（去 # 后 3 或 6 位）且无效才报错；其余情况静默清空输出
        var hex = value.Trim().TrimStart('#');
        var isCompleteInput = hex.Length is 3 or 6;

        if (isCompleteInput && !TryParseHex(HexInput, out var color))
        {
            HasErrors = true;
            ErrorMessage = "无效的 HEX 颜色值";
            return;
        }

        HasErrors = false;
        ErrorMessage = null;
        ConvertFromHex();
    }

    private void ConvertFromHex()
    {
        if (TryParseHex(HexInput, out var color))
        {
            RgbOutput = $"rgb({color.R}, {color.G}, {color.B})";
            var hsl = RgbToHsl(color.R, color.G, color.B);
            HslOutput = $"hsl({hsl.H}, {hsl.S}%, {hsl.L}%)";
            PreviewColor = color;
        }
    }

    [RelayCommand]
    private void Reset()
    {
        HexInput = "#3B82F6";
        HasErrors = false;
        ErrorMessage = null;
    }

    private static bool TryParseHex(string hex, out Color color)
    {
        color = default;

        if (string.IsNullOrWhiteSpace(hex))
            return false;

        var cleanHex = hex.Trim().TrimStart('#');

        if (cleanHex.Length == 3)
        {
            cleanHex = new string(new[]
            {
                cleanHex[0], cleanHex[0],
                cleanHex[1], cleanHex[1],
                cleanHex[2], cleanHex[2]
            });
        }

        if (cleanHex.Length != 6 || !uint.TryParse(cleanHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            return false;

        color = Color.FromRgb((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));
        return true;
    }

    private static (int H, int S, int L) RgbToHsl(byte r, byte g, byte b)
    {
        var rf = r / 255f;
        var gf = g / 255f;
        var bf = b / 255f;

        var max = MathF.Max(rf, MathF.Max(gf, bf));
        var min = MathF.Min(rf, MathF.Min(gf, bf));
        var delta = max - min;

        float h = 0, s = 0;
        var l = (max + min) / 2f;

        if (delta != 0)
        {
            s = l > 0.5f ? delta / (2f - max - min) : delta / (max + min);

            if (max == rf) h = ((gf - bf) / delta + (gf < bf ? 6 : 0)) / 6f;
            else if (max == gf) h = ((bf - rf) / delta + 2) / 6f;
            else h = ((rf - gf) / delta + 4) / 6f;
        }

        return ((int)(h * 360), (int)(s * 100), (int)(l * 100));
    }
}
