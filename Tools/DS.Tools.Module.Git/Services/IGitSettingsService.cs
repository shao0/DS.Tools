using DS.Tools.Module.Git.Models;

namespace DS.Tools.Module.Git.Services;

/// <summary>
/// Git 模块本地设置服务 - 经源生成上下文将设置持久化为 JSON（AOT 兼容，零反射）。
/// 文件缺失/损坏时返回默认值；保存失败仅记日志，绝不拖垮应用。
/// </summary>
public interface IGitSettingsService
{
    /// <summary>
    /// 加载本地设置（文件缺失/损坏/反序列化失败时返回默认值）
    /// </summary>
    GitSettings Load();

    /// <summary>
    /// 保存本地设置到磁盘（目录自动创建）
    /// </summary>
    void Save(GitSettings settings);
}
