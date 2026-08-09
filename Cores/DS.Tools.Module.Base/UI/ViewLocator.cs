using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CommunityToolkit.Mvvm.ComponentModel;
using DS.Tools.Core.Models;
using System.Runtime.CompilerServices;

namespace DS.Tools.Module.Base.UI;

/// <summary>
/// View 定位器 - 基于约定的自动 ViewModel→View 映射（AOT 兼容方案）
///
/// 约定规则：
/// 1. ViewModel 以 "ViewModel" 结尾
/// 2. View 以 "View" 结尾
/// 3. View 和 ViewModel 在同一命名空间，仅后缀不同
///
/// AOT 兼容性：模块通过 RegisterViewMapping 显式注册映射，避免运行时字符串类型查找
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    // 动态注册的映射表（各模块在初始化时注册）
    private static readonly Dictionary<Type, Type> ViewModelToViewMap = new();

    /// <summary>
    /// 注册 ViewModel-View 映射（由各模块调用）
    /// </summary>
    public static void RegisterViewMapping(Type viewModelType, Type viewType)
    {
        ArgumentNullException.ThrowIfNull(viewModelType);
        ArgumentNullException.ThrowIfNull(viewType);

        if (!typeof(ViewModelBase).IsAssignableFrom(viewModelType))
        {
            throw new ArgumentException($"Type {viewModelType.Name} must inherit from ViewModelBase");
        }

        if (!typeof(Control).IsAssignableFrom(viewType))
        {
            throw new ArgumentException($"Type {viewType.Name} must inherit from Control");
        }

        ViewModelToViewMap[viewModelType] = viewType;
    }

    /// <summary>
    /// 构建 View 实例（接受 ViewModel 数据）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Control? Build(object? data)
    {
        if (data is null)
            return null;

        // 仅处理 ViewModel（ObservableObject 的子类）
        if (data is not ObservableObject viewModel)
            return null;

        // 从注册映射表查找 View 类型
        var viewModelType = viewModel.GetType();
        if (!ViewModelToViewMap.TryGetValue(viewModelType, out var viewType))
        {
            return CreateNotFoundControl(viewModelType.Name);
        }

        // 创建 View 实例（AOT 兼容：使用无参构造）
        try
        {
            return (Control?)Activator.CreateInstance(viewType);
        }
        catch
        {
            return CreateNotFoundControl(viewModelType.Name);
        }
    }

    /// <summary>
    /// 判断是否匹配此数据模板（所有 ViewModel 都匹配）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Match(object? data)
    {
        return data is ObservableObject;
    }

    /// <summary>
    /// 创建"未找到"的占位控件
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Control CreateNotFoundControl(string viewModelName)
    {
        return new TextBlock
        {
            Text = $"View not found for: {viewModelName}",
            Margin = new Avalonia.Thickness(16)
        };
    }
}