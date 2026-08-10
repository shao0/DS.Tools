# DS.Tools 项目文档

> 本文档仅描述**当前代码状态**（版本 2.0，2026-08-10：清除全部历史重构记录）。
> 若文档与代码不符，以源码为准。

## 📌 项目概览

跨平台桌面工具集。**.NET 10.0 + C# 14 + Avalonia 12.1.0**，完全兼容 NativeAOT（零运行时反射）。

## 🏗️ 架构设计

### 项目结构

```
DS.Tools.slnx
├── DS.Tools/                    # 主应用（Avalonia UI，组合根：模块注册/Serilog/主题；含主页 Dashboard）
├── Cores/
│   ├── DS.Tools.Core/          # 统一核心层（接口/模型/服务/DI，仅依赖 MEL 抽象与 Avalonia 基础类型）
│   ├── DS.Tools.Module.Base/   # 工具模块基类（IToolModule/IToolRegistry/IToolCatalog/INavigationService）
│   └── DS.Tools.UI.Shared/     # 共享 UI 资源（图标/样式/控件）
├── Tools/
│   ├── DS.Tools.Module.Text/   # 文本工具模块（6 个子工具）
│   └── DS.Tools.Module.Git/    # Git 日志模块（git-log）
└── Tests/DS.Tools.Tests/       # 单元测试（235 个，含 Headless UI 集成测试）
```

### 技术栈

| 组件 | 版本/配置 |
|---|---|
| 目标框架 | `net10.0`，`LangVersion=latest`，Nullable + ImplicitUsings |
| UI | Avalonia 12.1.0（Desktop/Themes.Fluent/Fonts.Inter/Headless；`AvaloniaUseCompiledBindingsByDefault=true`） |
| MVVM | CommunityToolkit.Mvvm 8.4.0（源生成器） |
| DI/配置 | Microsoft.Extensions.* 10.0.0（DI/Configuration/Json/Binder/Logging/Options；`IServiceCollection` 显式注册，禁反射扫描） |
| 日志 | Serilog 4.4.0 + Extensions.Logging 9.0.0 + Sinks.File 6.0.0 + Sinks.Console 5.0.0（级别/文件滚动读 `appsettings.json` 的 `Logging` 节） |
| 序列化 | System.Text.Json 源生成上下文（禁 Newtonsoft.Json） |
| 测试 | xUnit 2.9.2 + FluentAssertions 7.0.0 + Moq 4.20.2 + Microsoft.NET.Test.Sdk 17.12.0 + Avalonia.Headless 12.1.0 |

### 模块化架构（极简，AOT 全程无反射、无 Type 键）

- **`IToolModule` / `ToolModule`**：模块契约——abstract 成员：`Id`/`Name`/`Icon`/`Description`/`CreateMainViewModel(IServiceProvider)`/`Register(IServiceCollection)`/`Initialize(IServiceProvider)`
- **`ToolRegistry`**：模块注册表（编译期显式注册；`Register` 时挂载 `IToolCatalog` 到模块基类，`SubTools`/`CreateSubToolViewModel` 经目录查询）
- **`ISubTool`**（C# 14 静态抽象接口）：子工具元数据的**单一事实来源**——ViewModel 以显式接口实现声明 `ModuleId`/`Id`/`Name`/`Icon` 四个静态属性，注册端经 **constrained call 编译期读取**（零实例化、零反射）
- **`ToolRegistration`**（注册扩展）：`AddSubTool<TVM, TView>()`——一行完成子工具 VM/View 入容器（Transient）、元数据、View 映射；`AddViewMapping<TVM, TView>()`——应用级页面仅映射（如主页）
- **`SubToolInfo`**：单一注册条目 = 元数据 + VM 工厂 `Func<IServiceProvider, ViewModelBase>` + 内嵌 View 映射委托（`MatchView`/`BuildView`），自带 `ModuleId`（`GetFullNavigationId()` 拼 `moduleId:subToolId`）
- **`IToolCatalog` / `ToolCatalog`**：查询侧——注入 `IEnumerable<SubToolInfo>` 单集合，按模块分组索引；`GetSubTools(moduleId)`/`GetSubTool(moduleId, subToolId)` 纯 string 键匹配；View 匹配为类型模式委托（后注册者优先）
- **`ViewRegistryDataTemplate`**：桥接 Avalonia `IDataTemplate` 与 `IToolCatalog`（MainWindow 构造时挂载，内容区 `ContentControl` 按目录渲染，无手写 XAML DataTemplate 列表）
- **`INavigationService`**：`NavigateTo(module, subTool)`×2 + `NavigationChanged`（无历史栈）
- **ViewModel 缓存**：`MainWindowViewModel` 按 `(toolId, subToolId)` 缓存复用 VM 实例（切工具保留输入状态）；主页缓存在 `("__home__", null)` 键（主页不属于任何模块，不经过 NavigationService）

### 模块注册范式

```csharp
// App.axaml.cs —— 组合根
private static readonly IToolModule[] ToolModules = [new TextModule(), new GitModule()];
```

模块 `Register` 内：

```csharp
services.AddSubTool<JsonFormatterViewModel, JsonFormatterView>();  // 一行完成接线
services.AddSingleton<IJsonFormatterService, JsonFormatterService>();
```

组合根只加主页映射 `services.AddViewMapping<DashboardViewModel, DashboardView>()`。**新增模块 = `ToolModules` 数组加一行。**

### 工具清单

| 模块 | 子工具 ID | 说明 |
|---|---|---|
| 主页（DS.Tools） | — | 功能总览：时钟/日期/时间戳/功能数卡片 + 模块分组磁贴导航（左上角图标回主页） |
| Text（`text-tools`） | `json-formatter` `base64-converter` `color-converter` `timestamp-converter` `password-generator` `text-hasher` | 文本处理工具集（ID 常量集中于 `TextModule.ToolIds`） |
| Git（`git-tools`） | `git-log` | 文件夹选择 → 根仓库分支 → 设置持久化（`%LocalAppData%\DS.Tools\git-settings.json`）→ 时间段日志（git CLI，控制符分隔解析，30s 超时 + 进程树 Kill）；**自动发现嵌套子仓库**（子模块/工作树/嵌套独立仓库，DFS 发现 `.git` 目录/文件，不进入 `.git` 内部、跳过符号链接防环、50 仓库/20 万目录上限），**每个仓库独立 Tab 分组**（根仓库第一、子仓库按相对路径排序，Tab 显示名 + 条数），Tab 切换查看；复制跟随当前选中仓库（整批/单条，内容为**完整 %B 消息**含正文与换行），子仓库失败仅跳过；默认时间范围本周一至周日 |

## ⛔ AOT 纪律（不可协商）

1. **零运行时反射**：禁 `Activator`、`Type.GetType`、`GetCustomAttribute`、`GetMethod/GetProperty`、`Assembly.*` 扫描、`DispatchProxy`
2. **禁止 Type 键创建对象**：一切实例经 DI 容器——强类型工厂 `Func<IServiceProvider, T>`（内部 `GetRequiredService<T>()`）；ViewModel 创建走 IoC 工厂，禁 `GetService(Type)`
3. **编译绑定**：所有绑定有 `x:DataType`；csproj 已设 `AvaloniaUseCompiledBindingsByDefault=true`；禁 `{ReflectionBinding}`
4. **View 映射 = 编译期模板/注册表**：`AddSubTool`/`AddViewMapping` 泛型注册 + `ViewRegistryDataTemplate`；禁 ViewLocator/字符串约定/`Activator` 实现
5. **序列化**：只用 System.Text.Json 源生成上下文（`[JsonSerializable]`）
6. **MVVM**：只用 CommunityToolkit.Mvvm 源生成器（`[ObservableProperty]`/`[RelayCommand]`）；禁 ReactiveUI
7. **验证标准**：`dotnet build` 在 `TreatWarningsAsErrors` + `EnableTrimAnalyzer` 下**零警告**（无 IL2xxx/IL3xxx）

## 🧩 Avalonia 12.1.x 已知框架坑与规避

### ⚠️ Wrap + 空段落布局死循环（严重，勿回退规避）

**现象**：`TextWrapping="Wrap"` 渲染**含空段落的文本**（如 git 提交消息正文的 `\n\n` 空行）时，`TextLayout.CreateTextLines` 断行循环不终止——每秒分配约 13 万个 TextLineImpl/GlyphRun 对象（45 秒内存暴涨 7.2GB），UI 永久挂起。**真实应用与 headless 同样触发**，与字体无关。最小复现：`new TextLayout("a\n\nb", typeface, 13, textWrapping: TextWrapping.Wrap)`。12.1.0/12.1.1 均存在。

**规避**：`GitLogMessageConverter`（`Tools/DS.Tools.Module.Git/Converters/`）显示层压缩 `\n{2,}`→`\n`，挂在 GitLogView 提交消息/错误消息两个 Wrap 绑定上；**复制仍读 VM 原始 Message**（含空段落）。

**规则**：凡是 `TextWrapping="Wrap"` 绑定**用户可控文本**的 TextBlock，显示层必须防空段落（机器生成输出——Base64/JSON/哈希——不含空段落，安全）。上游修复后可移除 converter。

### 动画 animator 机制

- Avalonia 12 animator 按属性类型静态匹配：**不可动画化整个 `RenderTransform` 对象**（`ITransform` 无匹配 animator，激活即抛异常中断渲染）；`Setter` 不支持嵌套属性路径（`RenderTransform.Angle` 无法解析）
- 用 `Opacity` 等 double 属性动画，或注册自定义 animator

### 已验证 API 事实

- `CalendarDatePicker.SelectedDate` 是 `DateTime?`（VM 转 `DateTimeOffset` 用本地时区偏移）
- `Watermark` 已废弃 → 用 `PlaceholderText`（AVLN5001 警告会冒泡）
- xUnit v2 无运行时动态跳过（`Assert.Skip` 是 v3）——git 缺失时用发现期 `RequiresGitFact` 属性置 `Skip`

## 🔧 编码与构建约定

- **命令**：统一 `[RelayCommand]`（MVVM Toolkit 8.0 起无 `[AsyncRelayCommand]` 特性）；`[RelayCommand]` + `Task` + `CancellationToken` 参数自动生成可取消命令
- **复制语义**：复制走 VM 原始数据（如 git 完整消息），显示层转换不污染复制内容；成功提示 2s 自动清除（`ToolViewModelBase.CopyToClipboardAsync` 统一实现）；剪贴板错误统一 rethrow → 基类展示
- **样式资源**：单个控件/视图内联 `Styles` 超过 **3 个**必须抽取为同名资源文件（`{视图名}Styles.axaml`，`StyleInclude` 引用）；共享样式集中于 UI.Shared `Styles/ToolStyles.axaml`；class 统一 **kebab-case**（Avalonia class 区分大小写）；颜色一律 `DynamicResource` 引用 App 主题色板（Light/Dark `ThemeDictionaries`，约 25 个语义化键）
- **构建**：`TreatWarningsAsErrors` 全开；目录级 `Directory.Build.props` 必须 `Import` 根 props（`GetPathOfFileAbove`），否则丢失 LangVersion/Nullable/AOT 配置；版本经 `Directory.Packages.props` 集中管理
- **git 集成**：`Process.StandardOutput`/`StandardErrorEncoding` 设 `UTF8Encoding(false)` + `-c i18n.logOutputEncoding=UTF-8`（防中文乱码）；`--until` 是排他边界——结束日期传 `until.AddDays(1)` 才含当天
- **headless 测试**：所有 Headless UI 测试类须同 xUnit `Collection("HeadlessUi")` 串行（Avalonia 平台仅可初始化一次）；`dotnet test` 卡死先查内存（`--blame-hang-timeout`）
- **性能**：`JsonDocument.Parse` 单遍完成验证+输出+深度（`Stream.Null` 取深度）；`LogEntries` 等大列表整批替换 `[ObservableProperty]`（禁逐条 Add 触发 N 次 CollectionChanged）；正则静态编译缓存

## ✅ 当前验证结果

- **构建**：Rider / CLI `dotnet build` 均通过，`TreatWarningsAsErrors` + `EnableTrimAnalyzer` 全开零警告
- **测试**：**241/241** 通过（单元测试 + Headless UI 集成测试），覆盖：模块化服务（ToolCatalog/ToolRegistry/NavigationService/DI 注册）、Text/Git 模块全部 ViewModel 与服务、ToolViewModelBase 复制语义、GitLogMessageConverter、Git 嵌套子仓库分组（.git 目录/文件标记、损坏 gitdir 跳过、Tab 切换/选中仓库复制）、ThemeService、SerilogConfig/AvaloniaLogSink、Headless UI 渲染与导航
- **冒烟**：应用启动正常；Serilog 控制台+文件日志输出启动/模块初始化链路；主页/模块视图经注册表渲染无崩溃

## ⚠️ 遗留问题

- **Avalonia Wrap 空段落死循环**：依赖 12.1.x 上游修复；修复后可移除 `GitLogMessageConverter` 并恢复原测试断言语义
- **git 依赖**：git-log 子工具依赖系统安装的 git CLI（测试经 `RequiresGitFact` 跳过）

---

**文档版本**：2.1
**最后更新**：2026-08-10
