namespace DS.Tools.Core.Interfaces;

/// <summary>
/// 文件夹选择服务 - 经主窗口的 StorageProvider 打开系统文件夹选择对话框（Avalonia 12，AOT 兼容）。
/// 所有对话框操作必须在 UI 线程执行，由实现内 Dispatcher 桥接。
/// </summary>
public interface IFolderPickerService
{
    /// <summary>
    /// 打开系统文件夹选择对话框
    /// </summary>
    /// <param name="suggestedPath">建议起始位置（例如上次选择的文件夹），可为 null</param>
    /// <returns>选中文件夹的本地路径；用户取消或无窗口可用时返回 null</returns>
    Task<string?> PickFolderAsync(string? suggestedPath);
}
