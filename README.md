# DS.Tools

<div align="center">

🧰 **现代跨平台桌面工具集**

基于 .NET 10.0 + Avalonia UI + C# 14 的 NativeAOT 兼容架构

[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Avalonia](https://img.shields.io/badge/Avalonia-12.1.0-blue.svg)](https://avaloniaui.net/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

</div>

## 📖 项目简介

DS.Tools 是一个现代化的跨平台桌面工具集应用，采用最新的 .NET 技术栈构建，完全兼容 NativeAOT 编译。

### 🎯 核心特性

- ✅ **极简模块化架构**：`IToolModule` + `ToolRegistry` + `INavigationService`，ViewModel 由 DI 容器（IoC）经强类型工厂创建
- ✅ **完全 AOT 兼容**：零运行时反射（扫描验证），支持 NativeAOT 编译为原生二进制
- ✅ **编译期 View 映射**：`x:DataType` DataTemplate 实现 ViewModel→View 映射，无约定式字符串查找
- ✅ **现代 MVVM 模式**：基于 CommunityToolkit.Mvvm 源生成器
- ✅ **跨平台支持**：支持 Windows、macOS、Linux
- ✅ **高性能 UI**：Avalonia UI 提供流畅的用户体验

### 🛠️ 内置工具

1. **📊 Dashboard** - 实时信息展示和快速导航
2. **📝 JSON 格式化** - JSON 美化、压缩、验证工具
3. **🔐 Base64 编码** - Base64 编码/解码工具
4. **🎨 颜色转换器** - 颜色格式转换工具
5. **🔑 密码生成器** - 安全密码生成工具
6. **#️⃣ 文本哈希** - 文本哈希计算工具
7. **⏰ 时间戳转换** - Unix 时间戳转换工具

## 🚀 快速开始

### 环境要求

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- 任意支持 .NET 的 IDE (Visual Studio, Rider, VS Code)

### 构建和运行

```bash
# 克隆仓库
git clone https://github.com/yourusername/DS.Tools.git
cd DS.Tools

# 构建解决方案
dotnet build DS.Tools.slnx

# 运行应用
dotnet run --project DS.Tools/DS.Tools.csproj

# 发布 Release 版本
dotnet build DS.Tools.slnx --configuration Release
```

### 开发模式

```bash
# 启用开发工具 (按 F12 打开)
dotnet run --project DS.Tools/DS.Tools.csproj --configuration Debug
```

## 🏗️ 架构设计

### 技术栈

| 组件 | 技术选型 |
|------|----------|
| **UI 框架** | Avalonia 12.1.0 |
| **MVVM 框架** | CommunityToolkit.Mvvm 8.4.0 |
| **依赖注入** | Microsoft.Extensions.DependencyInjection 10.0.0 |
| **日志** | Serilog 4.2.0 |
| **配置** | Microsoft.Extensions.Configuration |
| **序列化** | System.Text.Json (Source Generator) |

### 项目结构

```
DS.Tools.slnx
├── DS.Tools/                      # 主应用 (Avalonia UI)
├── Cores/DS.Tools.Core/           # 统一核心层（接口/模型/服务/DI）
├── Cores/DS.Tools.Module.Base/    # 工具模块基类（IToolModule/ToolRegistry）
├── Cores/DS.Tools.UI.Shared/      # 共享 UI 资源
├── Tools/DS.Tools.Module.Text/    # 文本工具模块（7 个子工具）
└── Tests/DS.Tools.Tests/          # 单元测试
```

### 模块化架构

项目采用**极简模块化设计**（AOT 全程无反射、无 Type 键创建）：

- **`IToolModule` / `ToolModule`**：工具模块契约与抽象基类
- **`ToolRegistry`**：模块注册表（编译期显式注册；注册时挂载子工具目录到模块基类）
- **`INavigationService`**：导航服务，管理模块/子工具切换与历史记录
- **IoC ViewModel 创建**：模块提供 `Func<IServiceProvider, ViewModelBase>` 强类型工厂，
  经 DI 容器 `GetRequiredService<T>()` 解析实例（无 `Type` 键、无反射）
- **统一注册表服务**：`ToolRegistration`（`AddViewMapping`/`AddSubTool` 一行注册）+
  `IToolCatalog`/`ToolCatalog` 查询（View 映射 + 子工具目录），
  `ViewRegistryDataTemplate` 桥接 Avalonia 渲染（无手写 XAML DataTemplate 列表）

#### 添加新工具

1. 继承 `ToolModule` 基类
2. 实现必需的成员（Id、Name、Icon、Description、`CreateMainViewModel`）
3. 在 `Register` 方法中注册：
   - 子工具：`services.AddSubTool<TViewModel>()`（元数据由 ViewModel 实现 `ISubTool` 接口声明）
   - View 映射：`services.AddViewMapping<TViewModel, TView>()`
   - 服务：`services.AddSingleton<IXxxService, XxxService>()`
4. 在 `App.axaml.cs` 的 `ToolModules` 数组中追加一行：`new XxxModule()`

详细开发指南请参考 [CLAUDE.md](CLAUDE.md)。

## 🎨 UI 特性

- **深色/浅色主题**：支持 Light/Dark/System 三种主题模式
- **响应式布局**：适配不同窗口尺寸
- **流畅动画**：基于 Avalonia 的高性能渲染
- **可访问性**：支持键盘导航和屏幕阅读器

## 🔧 配置

应用程序配置文件 `appsettings.json`：

```json
{
  "Theme": {
    "DefaultTheme": "System",
    "FollowSystemTheme": true
  },
  "Logging": {
    "DefaultLevel": "Information",
    "WriteToFile": true
  },
  "Tools": {
    "DefaultToolId": "text-tools",
    "EnabledTools": [
      "dashboard",
      "json-formatter",
      "base64-converter",
      "color-converter",
      "timestamp-converter",
      "password-generator",
      "text-hasher"
    ]
  }
}
```

## 🧪 测试

```bash
# 运行测试
dotnet test Tests/DS.Tools.Tests/DS.Tools.Tests.csproj

# 查看测试覆盖率
dotnet test --collect:"XPlat Code Coverage"
```

## 📦 构建发布

### Windows

```bash
dotnet publish DS.Tools/DS.Tools.csproj -c Release -r win-x64 --self-contained
```

### macOS

```bash
dotnet publish DS.Tools/DS.Tools.csproj -c Release -r osx-x64 --self-contained
```

### Linux

```bash
dotnet publish DS.Tools/DS.Tools.csproj -c Release -r linux-x64 --self-contained
```

## 🤝 贡献指南

欢迎贡献！请遵循以下步骤：

1. Fork 本仓库
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 开启 Pull Request

### 代码规范

- 遵循 C# 编码约定
- 使用 `var` 声明显式类型
- 为公共成员添加 XML 文档注释
- 保持与现有代码风格一致

## 📄 许可证

本项目采用 MIT 许可证 - 查看 [LICENSE](LICENSE) 文件了解详情。

## 🙏 致谢

- [Avalonia UI](https://avaloniaui.net/) - 跨平台 UI 框架
- [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/) - MVVM 工具包
- [.NET](https://dotnet.microsoft.com/) - 开发平台

## 📞 联系方式

- 项目主页：[GitHub Repository](https://github.com/yourusername/DS.Tools)
- 问题反馈：[Issues](https://github.com/yourusername/DS.Tools/issues)
- 讨论区：[Discussions](https://github.com/yourusername/DS.Tools/discussions)

---

<div align="center">

**⭐ 如果这个项目对你有帮助，请给个 Star！**

Made with ❤️ by .NET Community

</div>