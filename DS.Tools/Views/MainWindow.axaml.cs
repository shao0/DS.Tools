using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DS.Tools.Views;

/// <summary>
/// 主窗口 - 应用程序主界面
/// AOT 兼容，编译绑定
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
    }
}
