using Avalonia.Controls;
using DS.Tools.Module.Base;

namespace DS.Tools.Module.Base.Interfaces;

/// <summary>
/// 工具目录接口（查询侧，统一注册表查询服务）-
/// 合并 View 映射查询与子工具查询：
/// 容器构建后经 MEL 集合注入（<c>IEnumerable&lt;SubToolInfo&gt;</c> 单条目类型）收集
/// 全部模块在 Register 阶段注册的元数据，按需查询。
/// AOT 兼容：无 Type 键、无反射——View 匹配为编译期类型模式，子工具为 string 键匹配。
/// </summary>
public interface IToolCatalog
{
    /// <summary>
    /// 判断 ViewModel 是否有注册的 View 映射
    /// </summary>
    /// <param name="viewModel">ViewModel 实例</param>
    /// <returns>已注册返回 true</returns>
    bool IsRegistered(object? viewModel);

    /// <summary>
    /// 获取 ViewModel 对应的 View（经 IoC 工厂创建，每次返回新实例）；未注册返回 null
    /// </summary>
    /// <param name="viewModel">ViewModel 实例</param>
    /// <returns>View 实例，未注册时返回 null</returns>
    Control? GetView(object? viewModel);

    /// <summary>
    /// 获取指定模块的全部子工具（未注册模块返回空列表）
    /// </summary>
    /// <param name="moduleId">模块 ID</param>
    /// <returns>子工具列表（保持注册顺序）</returns>
    IReadOnlyList<SubToolInfo> GetSubTools(string moduleId);

    /// <summary>
    /// 根据子工具 ID 获取子工具（未找到返回 null）
    /// </summary>
    /// <param name="moduleId">模块 ID</param>
    /// <param name="subToolId">子工具 ID</param>
    /// <returns>子工具信息，未找到时返回 null</returns>
    SubToolInfo? GetSubTool(string moduleId, string subToolId);
}
