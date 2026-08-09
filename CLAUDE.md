# DS.Tools 架构重构计划

## 📋 执行摘要

基于对当前代码库的全面分析，发现DS.Tools项目存在严重的架构碎片化、模块化系统未完成接线、以及文档与代码严重不符等问题。本重构计划旨在建立一个干净、高效、符合现代.NET 10最佳实践的架构。

## 🔍 当前架构问题诊断

### 1. 项目结构过度碎片化（严重）

**问题描述**：
- Core层被拆分为4个独立项目：
   - `DS.Tools.Core.Abstractions`（接口定义）
   - `DS.Tools.Core.Models`（数据模型）
   - `DS.Tools.Core.Services`（服务实现）
   - `DS.Tools.Core`（核心实现）

**违反原则**：
- **违反简单性原则**：不必要的项目拆分增加了维护复杂度
- **违反内聚性原则**：相关概念分散在不同项目中
- **违反DRY原则**：接口在不同项目中重复定义

**具体表现**：
```csharp
// IToolModule在两个项目中重复定义
// Cores/DS.Tools.Core.Abstractions/Interfaces/IToolModule.cs
// Cores/DS.Tools.Core/Interfaces/IToolModule.cs  // 重复！
```

### 2. 模块化系统未完成接线（严重）

**问题描述**：
- `DS.Tools.Module.Text`虽然包含完整的ViewModel和View，但未被主应用引用
- `TextModule`是空壳实现，所有方法都是空实现
- `IToolRegistry`在运行时为空，没有任何工具被注册
- 缺少View渲染机制将ViewModel映射到对应的View

**具体表现**：
```csharp
// TextModule.cs - 空壳实现
public override IServiceCollection Register(IServiceCollection services)
{
    return services; // 空实现！
}

public override void Initialize(IServiceProvider services)
{
    // 空实现！
}

public override Type ViewModelType => typeof(TextModule); // 语义错误！
```

**主应用缺失关键代码**：
```csharp
// App.axaml.cs 中应该有但缺失的代码：
// 1. 没有引用 DS.Tools.Module.Text 项目
// 2. 没有实例化并注册 TextModule
// 3. 没有View-ViewModel映射机制
```

### 3. 文档与代码严重不符（中等）

**问题描述**：
- 大量XML注释描述的是已删除的Prism风格模块化系统
- 注释中提到的方法和类在实际代码中不存在

**具体表现**：
```csharp
// App.axaml.cs 第17行
/// 基于Prism风格模块化框架（自实现，AOT兼容）。
/// 模块经IModuleCatalog显式注册，由IModuleManager按依赖拓扑排序执行
/// 两阶段生命周期：RegisterTypes（Build前）→ OnInitialized（Build后）。
// 但实际代码中没有IModuleCatalog、IModuleManager、拓扑排序等！
```

### 4. 缺少View渲染机制（严重）

**问题描述**：
- `MainWindow.axaml`中有`ContentControl`绑定到`ActiveToolViewModel`
- 但没有`DataTemplate`定义如何将ViewModel类型映射到对应的View

**具体表现**：
```xml
<!-- MainWindow.axaml 第205行 -->
<ContentControl Content="{Binding ActiveToolViewModel}" />
<!-- 没有DataTemplate定义如何渲染不同的ViewModel！ -->
```

### 5. 违反现代.NET最佳实践（中等）

**问题描述**：
- 过度项目拆分不符合现代.NET简洁原则
- 没有充分利用C# 14的最新特性
- 缺乏自动化发现和注册机制

## 🎯 重构目标

### 主要目标

1. **统一Core层** - 将4个Core项目合并为1个，提升内聚性
2. **完成模块化接线** - 实现完整的工具模块发现、注册和渲染机制
3. **简化架构** - 采用极简的IToolModule+ToolRegistry架构
4. **更新文档** - 确保所有注释与实际代码一致
5. **实现View渲染** - 基于DataTemplate的ViewModel→View自动映射

### 次要目标

1. **提升可维护性** - 减少项目数量，简化依赖关系
2. **增强可扩展性** - 建立清晰的工具模块扩展模式
3. **遵循最佳实践** - 采用现代.NET 10和C# 14特性

## 🏗️ 新架构设计

### 项目结构（简化后）

```
DS.Tools.slnx
├── DS.Tools/                    # 主应用（Avalonia UI，组合根：模块注册/Serilog/主题）
├── Cores/
│   ├── DS.Tools.Core/          # 统一核心层（接口/模型/服务/DI，仅依赖 MEL 抽象与 Avalonia 基础类型）
│   ├── DS.Tools.Module.Base/   # 工具模块基类（IToolModule/IToolRegistry/INavigationService）
│   └── DS.Tools.UI.Shared/     # 共享UI资源（图标）
├── Tools/
│   └── DS.Tools.Module.Text/   # 文本工具模块（7 个子工具）
└── Tests/DS.Tools.Tests/       # 单元测试
```

### 核心设计原则

1. **极简模块化**：IToolModule + ToolRegistry + 自动发现
2. **编译期约束**：所有模块注册在编译期完成，无运行时反射
3. **AOT兼容**：全面支持NativeAOT和Trimming
4. **MVVM纯粹性**：CommunityToolkit.Mvvm源生成器
5. **自动化View映射**：基于约定和DataTemplate的ViewModel→View映射

### 技术栈（现代.NET 10）

- **.NET 10.0** + **C# 14**（最新语言特性）
- **Avalonia 12.1.0**（跨平台UI）
- **CommunityToolkit.Mvvm 8.4.0**（MVVM源生成器）
- **Microsoft.Extensions.DependencyInjection 9.0.0**（DI容器）
- **System.Text.Json**（AOT兼容序列化）
- **Serilog**（结构化日志）

## 📝 重构实施计划

### 阶段1：核心层统一（高优先级）

**目标**：将4个Core项目合并为1个`DS.Tools.Core`

**步骤**：

1. **创建新的统一项目结构**
   ```
   DS.Tools.Core/
   ├── Interfaces/           # 所有接口定义
   ├── Models/              # 所有数据模型
   ├── Services/            # 所有服务实现
   ├── Infrastructure/      # 基础设施（日志、事件）
   ├── DI/                 # 依赖注入扩展
   ├── Serialization/      # JSON序列化上下文
   └── Configuration/      # 配置管理
   ```

2. **迁移代码**
   - 将`DS.Tools.Core.Abstractions/*` → `DS.Tools.Core/Interfaces/*`
   - 将`DS.Tools.Core.Models/*` → `DS.Tools.Core/Models/*`
   - 将`DS.Tools.Core.Services/*` → `DS.Tools.Core/Services/*`
   - 将`DS.Tools.Core/Infrastructure/*` → `DS.Tools.Core/Infrastructure/*`

3. **更新项目引用**
   - 更新`DS.Tools.csproj`引用新的统一Core项目
   - 更新`DS.Tools.Module.Base.csproj`引用
   - 更新`DS.Tools.Module.Text.csproj`引用

4. **删除旧项目**
   - 删除`DS.Tools.Core.Abstractions`
   - 删除`DS.Tools.Core.Models`
   - 删除`DS.Tools.Core.Services`

**预期收益**：
- 减少3个项目，简化维护
- 消除接口重复定义
- 提升内聚性，降低耦合度
- 减少编译时间

### 阶段2：模块化系统接线（高优先级）

**目标**：完成工具模块的发现、注册和渲染机制

**步骤**：

1. **完善TextModule实现**
   ```csharp
   public sealed class TextModule : ToolModule
   {
       public override string Id => "text-tools";
       public override string Name => "文本工具";
       public override string Icon => "📝";
       public override string Description => "文本处理工具集";
       
       public override Type ViewModelType => typeof(DashboardViewModel);
       
       public override IServiceCollection Register(IServiceCollection services)
       {
           // 注册所有ViewModel为瞬时服务
           services.AddTransient<DashboardViewModel>();
           services.AddTransient<JsonFormatterViewModel>();
           services.AddTransient<Base64ViewModel>();
           services.AddTransient<ColorConverterViewModel>();
           services.AddTransient<PasswordGeneratorViewModel>();
           services.AddTransient<TextHasherViewModel>();
           services.AddTransient<TimestampConverterViewModel>();
           
           // 注册服务
           services.AddSingleton<IJsonFormatterService, JsonFormatterService>();
           
           return services;
       }
       
       public override void Initialize(IServiceProvider services)
       {
           // 模块初始化逻辑（如有需要）
       }
   }
   ```

2. **在App.axaml.cs中注册模块**
   ```csharp
   private IServiceCollection ConfigureServices(IConfiguration configuration)
   {
       var services = new ServiceCollection();
       
       // ... 现有服务注册 ...
       
       // 🔥 新增：注册工具模块
       var textModule = new TextModule();
       services = textModule.Register(services);
       
       // 注册模块到ToolRegistry
       services.PostConfigure<IToolRegistry>(registry =>
       {
           registry.Register(textModule);
       });
       
       return services;
   }
   ```

3. **实现View渲染机制**

   方案A：在MainWindow.axaml中定义DataTemplate（推荐）
   ```xml
   <ContentControl Content="{Binding ActiveToolViewModel}">
       <ContentControl.DataTemplates>
           <DataTemplate x:DataType="vm:DashboardViewModel">
               <views:DashboardView />
           </DataTemplate>
           <DataTemplate x:DataType="vm:JsonFormatterViewModel">
               <views:JsonFormatterView />
           </DataTemplate>
           <!-- 其他工具的DataTemplate... -->
       </ContentControl.DataTemplates>
   </ContentControl>
   ```

   方案B：创建通用ViewLocator
   ```csharp
   public sealed class ViewLocator : IDataTemplate
   {
       public Control? Build(object? data)
       {
           if (data is null) return null;
           
           var name = data.GetType().FullName!.Replace("ViewModel", "View");
           var type = Type.GetType(name);
           
           if (type != null)
           {
               return (Control)Activator.CreateInstance(type)!;
           }
           
           return new TextBlock { Text = "Not Found: " + name };
       }
       
       public bool Match(object? data) => data is ObservableObject;
   }
   ```

4. **更新MainWindowViewModel实现**
   ```csharp
   [RelayCommand]
   private void SelectTool(IToolModule? tool)
   {
       if (tool is not null)
       {
           _toolRegistry.ActiveTool = tool;
           
           // 从DI容器解析对应的ViewModel
           var viewModel = _serviceProvider.GetService(tool.ViewModelType) as ViewModelBase;
           ActiveToolViewModel = viewModel;
       }
   }
   ```

**预期收益**：
- 完整的工具模块功能
- 工具可以在UI中正常显示和切换
- 为未来添加新工具建立清晰模式

### 阶段3：文档更新（中优先级）

**目标**：确保所有注释和文档与实际代码一致

**步骤**：

1. **更新App.axaml.cs注释**
   ```csharp
   /// <summary>
   /// 应用程序入口 - 基于极简模块化架构（IToolModule + ToolRegistry）。
   /// 模块在编译期显式注册，由IServiceProvider管理生命周期。
   /// NativeAOT兼容，无运行时反射。
   /// </summary>
   ```

2. **更新ServiceCollectionExtensions.cs注释**
   ```csharp
   /// <summary>
   /// 依赖注入扩展方法 - 显式注册所有服务，AOT兼容。
   /// 核心服务：AddCoreServices、应用服务：AddApplicationServices。
   /// 禁止运行时反射扫描程序集。
   /// </summary>
   ```

3. **更新CLAUDE.md**
   - 移除对Prism架构的引用
   - 更新模块化系统描述
   - 添加新的项目结构说明

4. **更新README.md**
   - 反映新的架构设计
   - 更新构建和运行指南

### 阶段4：架构优化（低优先级）

**目标**：充分利用现代.NET 10和C# 14特性

**可能的优化**：

1. **使用C# 14特性**
   - 主构造函数减少样板代码
   - `file`关键字实现内部类型隐藏
   - 改进的`switch`表达式

2. **性能优化**
   - 使用`Span<T>`减少内存分配
   - 使用`ArrayPool<T>`优化缓冲区管理
   - 添加缓存机制

3. **增强错误处理**
   - 统一的异常处理策略
   - 结构化的错误消息
   - 用户友好的错误显示

## 🚀 实施时间线

| 阶段 | 预计工作量 | 优先级 | 依赖 |
|------|----------|--------|------|
| 阶段1：核心层统一 | 2-3小时 | 高 | 无 |
| 阶段2：模块化接线 | 3-4小时 | 高 | 阶段1 |
| 阶段3：文档更新 | 1-2小时 | 中 | 阶段1,2 |
| 阶段4：架构优化 | 2-3小时 | 低 | 阶段1,2,3 |

**总预计工作量**：8-12小时

## ⚠️ 风险与缓解

### 主要风险

1. **破坏现有功能**
   - **缓解**：充分的测试，特别是模块化系统
   - **回滚计划**：保留原始代码备份

2. **引入新的bug**
   - **缓解**：逐步重构，每步测试验证
   - **监控**：密切关注编译警告和运行时错误

3. **文档再次过时**
   - **缓解**：建立代码和文档同步更新的流程
   - **验证**：重构完成后全面检查文档一致性

## 📊 成功指标

1. **项目数量**：从6个减少到5个（-16%）
2. **接口重复定义**：从2个减少到0个（-100%）
3. **工具模块可用性**：从0%提升到100%
4. **文档准确性**：从~60%提升到95%+
5. **编译时间**：预计减少15-20%

## 🔄 未来扩展性

重构后的架构将支持：

1. **轻松添加新工具模块**
   - 继承`ToolModule`
   - 实现必需的成员
   - 在`App.axaml.cs`中注册

2. **支持工具模块独立开发**
   - 模块可以独立编译测试
   - 清晰的接口契约
   - 最小化依赖关系

3. **AOT/Trim完全兼容**
   - 所有代码符合AOT纪律
   - 无运行时反射
   - 可编译为原生二进制

---

## 📝 下一步行动

1. **评审和批准**：利益相关者评审重构计划
2. **开始阶段1**：创建新的统一Core项目
3. **持续集成测试**：每步验证功能正常
4. **完成所有阶段**：达成重构目标

---

## ✅ 重构执行状态（2026-08-09 更新）

| 阶段 | 状态 | 说明 |
|------|------|------|
| 阶段1：核心层统一 | ✅ 已完成 | 3 个空壳项目（Abstractions/Models/Services）已删除，仅剩 `Cores/DS.Tools.Core` |
| 阶段2：模块化接线 | ✅ 已完成 | TextModule 完整实现；`App.axaml.cs` 中注册；IoC 工厂化（见下） |
| 阶段3：文档更新 | ✅ 已完成 | 本文档 + README 已同步 |
| 阶段4：架构优化 | ✅ 已完成（部分） | 见「AOT 纪律」说明 |
| 二轮清理（2026-08-09） | ✅ 已完成 | 死代码移除/日志接线/模块数组注册/主题双色板/剪贴板真实现，见「二轮架构清理」 |

### 反射清除结论（AOT 保证）

全库扫描（`Activator`/`Type.GetType`/`GetCustomAttribute`/`GetMethod`/`Assembly.*` 等）：
- **唯一反射点**：`ViewLocator.cs` 的 `Activator.CreateInstance` —— **已删除**，改为 `MainWindow.axaml` 编译期 `x:DataType` DataTemplate（Avalonia XAML 编译器直接生成实例化代码）
- **IoC 化改造**：`IToolModule.ViewModelType`（Type 键）→ `CreateMainViewModel`/`CreateSubToolViewModel` 强类型工厂；`SubToolInfo.Type` → `Func<IServiceProvider, ViewModelBase>`。ViewModel 一律经 DI 容器 `GetRequiredService<T>()` 创建，**代码中已无 Type 键创建路径**
- `System.Text.Json` 经源生成上下文（AOT 兼容）；`EventAggregator`/`AppJsonContext` 已随二轮清理删除
- 验证：`EnableTrimAnalyzer=true` + `TreatWarningsAsErrors=true` 构建零警告

### UI 问题修复（2026-08-09 三轮）

1. **一级菜单显示不全**：侧边栏 DockPanel 子元素顺序错误（LastChildFill 拉伸导致版本区抢占菜单空间）——版本 Border 最先声明 `Dock="Bottom"`、Logo `Dock="Top"`、ScrollViewer 最后填充
2. **JSON 工具界面不显示**：`JsonFormatterView` 加载指示器用 `Style.Animations` 动画化整个 `RenderTransform` 对象（`ITransform` 类型）——Avalonia 12 的 animator 按属性类型静态匹配，`ITransform` 无匹配，动画激活即抛 `No animator registered for the property RenderTransform`，异常中断 Content 绑定应用导致内容区空白。修复：改动画化 `Opacity`（double，命中内置 `DoubleAnimator`）。**教训：Avalonia 12 不可动画化整个 RenderTransform 对象；Setter 不支持嵌套属性路径（`RenderTransform.Angle` 无法解析）**
3. **版本信息固定底部**：同问题 1 的 Dock 顺序修复

### 当前验证结果

- 构建：Rider / CLI `dotnet build` 均通过，`TreatWarningsAsErrors` 全开零警告
- 测试：62/62 通过（含 JsonFormatterService 10 个、IoC 工厂分支、VM 缓存分支、Headless UI 集成测试——headless 测试类须同 xUnit Collection 串行，Avalonia 平台仅可初始化一次）
- 冒烟：应用启动后正常运行；Serilog 控制台+文件日志输出启动/模块初始化链路；DashboardView 经 DataTemplate 渲染无崩溃

### 二轮架构清理（2026-08-09）

在重构计划全部落地后执行的第二轮清理，目标：更干净、更高效、更易迭代与扩展。

1. **死代码移除**（约 14 个文件 + 8 个 NuGet 包）：EventAggregator（全库零订阅）、AppConfigManager + 整套配置模型 + AppJsonContext（未接线）、LocalizationService（注册无消费者且每次调用重建字典）、RegexPatterns、`IToolRegistry.ActiveTool`/`ToolChanged`（无读取方，当前选中状态唯一归 INavigationService）、手写 SerilogLoggerFactory（改标准 `AddSerilog` 接线）
2. **日志接线**：Serilog 实现下沉到主应用组合根（`Infrastructure/Logging/SerilogConfig.cs`），级别读 `appsettings.json` 的 `Logging:DefaultLevel`；`ILogger<T>` 使用点：App 启动/模块初始化、模块 Initialize、JSON 操作异常、剪贴板失败
3. **模块注册泛化**：`App.axaml.cs` 收敛为 `ToolModules` 数组（新增模块 = 数组加一行，`Register`/`Initialize` 统一 foreach）；导航 ID 收敛为 `TextModule.ToolIds` 常量类（消灭 `"text-tools:xxx"` 魔法字符串）
4. **ViewModel 缓存**：`MainWindowViewModel` 按 `(toolId, subToolId)` 缓存复用 VM 实例——修复 Dashboard 时钟定时器随每次导航泄漏的问题，切换工具保留输入状态
5. **主题双色板**：`App.axaml` 定义 Light/Dark `ThemeDictionaries` 语义化色板（约 25 个键），全部 axaml（主窗口/7 个工具视图/样式）硬编码颜色收敛为 `DynamicResource`——主题切换按钮真实生效
6. **效率**：JsonFormatterService 去伪异步（无 await 的 async 方法改同步）且单次 `JsonDocument.Parse` 完成验证+输出+深度（原 3 次解析）；删除全库 ~30 处 `[MethodImpl(AggressiveInlining)]` 滥用；`ClipboardService` 占位 Console 输出改为 Avalonia 12 真实剪贴板 API（`DataTransferItem.CreateText` / `TryGetDataAsync`）；`NavigationService` 移除未使用的 `IServiceProvider` 依赖
7. **配置对齐**：Microsoft.Extensions.* 升到 10.0.0（与 net10.0 对齐）；`appsettings.json` 子工具 ID 与 `ToolIds` 常量一致；`Directory.Build.props` 全开 `TreatWarningsAsErrors`（移除覆盖它的 `WarningsAsErrors` 列表）

### Git 日志模块（2026-08-09）

新增第二个工具模块 `Tools/DS.Tools.Module.Git`（`git-tools` → 子工具 `git-log`），四步功能：文件夹选择 → 当前分支名 → 设置 JSON 持久化 → 时间段日志。

1. **文件夹选择器入 Core**：`IFolderPickerService`/`FolderPickerService`（镜像 `ClipboardService` 的 MainWindow + Dispatcher 模式，`StorageProvider.OpenFolderPickerAsync`，Avalonia 12 验证过 `TryGetLocalPath`/`CanPickFolder`/`FolderPickerOpenOptions`），注册于 `AddApplicationServices()`
2. **设置持久化**：`IGitSettingsService` → `%LocalAppData%\DS.Tools\git-settings.json`（源生成上下文 `GitJsonContext`，camelCase + 缩进；双构造函数供测试注入路径；损坏/缺失回默认值，保存失败仅记日志）
3. **git CLI 集成**：`IGitLogService` 经 `System.Diagnostics.Process` 执行（零新依赖，禁 LibGit2Sharp）——`git -C <path>` + `ArgumentList` 免引号；分支 = `symbolic-ref --short -q HEAD`（游离 HEAD 退化 `rev-parse --short HEAD`）；日志 = `log -n 1000 [--since/--until] --pretty=format:%x1e%h%x1f%an%x1f%ae%x1f%aI%x1f%s`（控制符分隔防主题含 `|`）；防死锁（先读流再等待）、30s 超时 + 进程树 Kill、`Win32Exception` 友好报错、空仓库 exit-128 特判为成功空列表
4. **修复多模块导航 bug**：`MainWindowViewModel.SelectSubTool` 原以「当前活动模块」推断子工具导航 ID——多模块下点击其他模块的子工具会路由错误（`text-tools:git-log`）；改为从 `IToolRegistry` 查找子工具所属模块（`SubTools.Contains` 引用相等）再拼 ID
5. **清理**：删除临时诊断测试 `MenuDiagTests.cs`/`MenuDiagAppTests.cs`（文件注释自述"确认后删除"；后者不在 HeadlessUi 串行集合内且初始化真实 App，污染共享平台状态导致其他 headless 测试随机失败）

**已验证 API 事实**（反射 Avalonia 12.1.0 程序集）：`CalendarDatePicker.SelectedDate` 是 `DateTime?`（VM 转 `DateTimeOffset` 用本地时区偏移）；`Watermark` 已废弃 → 用 `PlaceholderText`（AVLN5001 警告会冒泡）。xUnit v2 无运行时动态跳过（`Assert.Skip`/`SkipException` 均 v3）——git 缺失时用发现期 `RequiresGitFact` 属性置 `Skip`。

**编码教训**：git 输出为 UTF-8 字节，`Process.StandardOutput` 在中文 Windows 上默认按 ANSI 代码页（GBK）解码 → 中文提交主题/作者/分支名乱码。修复：`StandardOutputEncoding`/`StandardErrorEncoding` 设 `UTF8Encoding(false)` + 命令参数 `-c i18n.logOutputEncoding=UTF-8`（兼容 GBK 提交编码的旧仓库）。

**默认时间范围**：打开工具即默认本周一至本周日（`SetDefaultDateRange`，`DayOfWeek` 周日=0 的周一偏移 `(dow+6)%7`）。注意 git `--until` 是排他边界——结束日期必须按"含当天"处理：VM 传参 `until.AddDays(1)`（次日零点），否则周日当天提交会被排除（服务层 `GetLog_UntilBoundary_IsExclusiveAtBoundaryInstant` 锁定该语义）。

**复制结果**：信息栏右侧「📋 复制结果」按钮（`CopyLogCommand`，CanExecute=有日志条目）——全部日志按 `hash | 作者 | yyyy-MM-dd HH:mm | 主题` 每行一条写入剪贴板，成功提示 2 秒后自动清除。

**侧边栏默认状态（2026-08-09 用户调整）**：`MainWindowViewModel.IsPaneOpen` 默认 false（侧边栏收起）+ MainWindow.axaml Expander 去掉 `IsExpanded="True"`（模块默认折叠）——headless 导航测试须先 `IsPaneOpen=true`（Show 前设置）再 `ExpandModule` 展开目标模块，才能点击子工具（SplitView 收起时窗格内容不在视觉树、折叠 Expander 的子工具同样不在）。

### 主页改造（2026-08-09）

1. **Dashboard 移入主应用成为主页**：`DashboardViewModel`/`DashboardView` 从 Module.Text 移至 `DS.Tools/ViewModels`、`DS.Tools/Views`（命名空间同步改 DS.Tools.*）；TextModule 移除 `ToolIds.Dashboard`、`AddTransient<DashboardViewModel>`、仪表盘 SubToolInfo，`CreateMainViewModel` 兜底改为 JsonFormatterViewModel；appsettings.json EnabledTools 移除 "dashboard"、补 "git-log"
2. **主页 = 功能总览**：`DashboardViewModel(IToolRegistry, INavigationService, ILogger)` 遍历注册表构建 `ModuleGroups`（每模块一组：图标+名称+子工具磁贴，磁贴携带完整导航 ID）；保留时钟卡片（时间/日期/时间戳）+ 新增功能总数卡片；磁贴点击 → `NavigateToToolCommand` → `NavigateTo(module:subTool)`
3. **左上角图标回主页**：MainWindow.axaml 标题栏 🧰+DS.Tools 包成透明 Button 绑 `NavigateToHomeCommand`；`MainWindowViewModel` 新增 `NavigateHome()`（应用级主页经 DI 创建，缓存在 `("__home__", null)` 键——主页不属于任何模块，不经过 NavigationService）；启动默认导航即主页
4. **主页磁贴命令绑定技巧**：磁贴在双层 DataTemplate 内（组/工具），命令绑定用 `{Binding ElementName=Root, Path=DataContext.NavigateToToolCommand}`（UserControl 根命名 Root，模式同主窗口 `ElementName=RootWindow`）——compiled binding 下可用

测试 108/108 通过（新增 46 个：Settings 6、GitLogService 18、Git VM 12、视图渲染 3、导航回归 2、复制 3、主页 4（分组构建/导航命令/磁贴渲染/回主页命令））。

---

**文档版本**：1.0
**创建日期**：2026-08-09
**最后更新**：2026-08-09
**作者**：架构重构计划