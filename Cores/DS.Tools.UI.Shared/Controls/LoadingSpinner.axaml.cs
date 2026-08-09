using Avalonia.Controls;

namespace DS.Tools.UI.Shared.Controls;

/// <summary>
/// 加载指示器控件 - 双层 Ellipse 旋转脉冲动画。
/// 供各工具视图复用（原 JsonFormatterView/GitLogView 内联标记去重）。
/// </summary>
public sealed partial class LoadingSpinner : UserControl
{
    public LoadingSpinner()
    {
        InitializeComponent();
    }
}
