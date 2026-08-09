using CommunityToolkit.Mvvm.ComponentModel;

namespace DS.Tools.Core.Models;

/// <summary>
/// ViewModel 基类 - 基于 CommunityToolkit.Mvvm 的 ObservableObject
/// 通过源生成器实现 INotifyPropertyChanged，NativeAOT/Trim 兼容（无运行时反射）
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    /// <summary>ViewModel 显示名称</summary>
    [ObservableProperty] private string _displayName = string.Empty;

    /// <summary>是否正在加载</summary>
    [ObservableProperty] private bool _isLoading;

    /// <summary>是否有错误</summary>
    [ObservableProperty] private bool _hasErrors;

    /// <summary>错误消息</summary>
    [ObservableProperty] private string? _errorMessage;
}