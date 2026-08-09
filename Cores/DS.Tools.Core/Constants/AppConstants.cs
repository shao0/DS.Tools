namespace DS.Tools.Core.Constants;

/// <summary>
/// 应用程序常量
/// </summary>
public static class AppConstants
{
    /// <summary>
    /// 应用名称
    /// </summary>
    public const string AppName = "DS.Tools";

    /// <summary>
    /// 应用版本
    /// </summary>
    public const string AppVersion = "1.0.0";

    /// <summary>
    /// 默认工具ID
    /// </summary>
    public const string DefaultToolId = "text-tools";

    /// <summary>
    /// 配置文件名
    /// </summary>
    public const string ConfigFileName = "appsettings.json";

    /// <summary>
    /// 窗口默认宽度
    /// </summary>
    public const double DefaultWindowWidth = 1200;

    /// <summary>
    /// 窗口默认高度
    /// </summary>
    public const double DefaultWindowHeight = 800;

    /// <summary>
    /// 窗口最小宽度
    /// </summary>
    public const double MinWindowWidth = 800;

    /// <summary>
    /// 窗口最小高度
    /// </summary>
    public const double MinWindowHeight = 600;

    /// <summary>
    /// 正则表达式模式
    /// </summary>
    public static class RegexPatterns
    {
        /// <summary>
        /// JSON 模式（简化版）
        /// </summary>
        public const string JsonPattern = @"^\s*\{.*\}\s*$";

        /// <summary>
        /// Base64 模式
        /// </summary>
        public const string Base64Pattern = @"^[A-Za-z0-9+/]*={0,2}$";

        /// <summary>
        /// 颜色 HEX 模式
        /// </summary>
        public const string HexColorPattern = @"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$";
    }
}