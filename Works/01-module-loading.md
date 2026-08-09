# 01 · 模块加载方式设计

> 关联文档:[02-View 与 ViewModel 注册](02-view-viewmodel-registration.md) · [03-模块 IoC 注册](03-module-ioc-registration.md) · [04-导航功能架构](04-navigation-architecture.md) · [总览](README.md)
>
> 适用范围:`Cores/DS.Tools.Core`、`Cores/DS.Tools.Module.Base`、主应用 `App.axaml.cs`

## 1. 背景与现状

代码库在 2026-07 的重构中删除了早期 Prism 风格模块化系统(`IModuleCatalog` / `ModuleManager` / `ModuleDependencySorter` / `ContainerRegistry` / `ViewRegistry` / `ModuleViewLocator` 等),替换为极简的 `IToolModule` + `ToolRegistry`,但**模块加载与注册的编排尚未补回**,导致:

- `App.axaml.cs` 的 `OnFrameworkInitializationCompleted`(`App.axaml.cs:36`)在 `ConfigureServices` 后直接 `BuildServiceProvider()`,**没有任何代码把模块注册进 `IToolRegistry`**。
- `IToolRegistry.Tools` 运行时为空 → `MainWindowViewModel` 构造时 `Tools = new(registry.Tools)` 为空、`GetTool("dashboard")` 返回 `null`。
- 唯一的模块 `DS.Tools.Module.Text.TextModule` 是空壳:`Register` 直接 `return services;`、`Initialize` 空体、`ViewModelType => typeof(TextModule)`,且主应用 `DS.Tools.csproj` 根本不引用该项目。

现有可复用的契约:

```csharp
// Cores/DS.Tools.Core/Interfaces/IToolModule.cs(已存在)
public interface IToolModule
{
    string Id { get; }
    string Name { get; }
    string Icon { get; }
    string Description { get; }
    Type ViewModelType { get; }
    IServiceCollection Register(IServiceCollection services);   // ← 签名将演进
    void Initialize(IServiceProvider services);
}

// Cores/DS.Tools.Module.Base/ToolModule.cs(已存在,abstract 7 个成员)
// Cores/DS.Tools.Core/Services/ToolRegistry.cs(已存在,运行期注册表)
```

## 2. 目标

1. **编译期显式**枚举所有模块(零反射扫描,AOT 合规)。
2. 模块实例化发生在 `BuildServiceProvider` **之前**,模块构造函数**不依赖任何 DI 服务**;需要 DI 的逻辑放在 `Initialize`。
3. 提供**两阶段生命周期**:`Register`(Build 前,只注册不解析) → `Initialize`(Build 后,可解析服务)。
4. 自动把每个 `IToolModule` 登记进 `IToolRegistry`(供侧边栏渲染与导航)。
5. 可选支持模块间依赖声明与拓扑加载顺序(单一模块用不上,但为未来扩展留口)。

## 3. 设计约束(AOT / Trim 红线)

| ❌ 禁止 | ✅ 替代 |
|---|---|
| `Assembly.LoadFrom` / `AppDomain.GetAssemblies` / `DependencyContext` 扫描 | 主应用编译期 `catalog.AddModule(() => new XxxModule())` |
| `Activator.CreateInstance(type)` 实例化模块 | 工厂委托 `Func<IToolModule>`,`() => new XxxModule()` |
| 模块构造函数注入 DI 服务 | 构造无参;服务在 `Register` 注册、`Initialize` 解析 |
| `AddSingleton(Type, Type)` 反射重载 | 泛型 / 工厂委托重载 |

## 4. 设计

### 4.1 演进 `IToolModule` 的 `Register` 签名

`Register` 当前只接收 `IServiceCollection`,但模块在同一阶段还需注册 **View 映射**与**导航条目**(见 02/04)。引入聚合上下文 `ModuleContext`,签名演化为:

```csharp
namespace DS.Tools.Core.Interfaces;

public interface IToolModule
{
    // 元数据(不变)
    string Id { get; }
    string Name { get; }
    string Icon { get; }
    string Description { get; }
    Type ViewModelType { get; }

    /// <summary>阶段①:BuildServiceProvider 之前。只做"注册",不得解析服务。</summary>
    void Register(ModuleContext context);

    /// <summary>阶段②:BuildServiceProvider 之后。可从 <paramref name="services"/> 解析服务做初始化。</summary>
    void Initialize(IServiceProvider services);
}
```

> 这是 breaking change,但当前所有 `Register` 实现都是空体(仅 `TextModule` 一处 `return services;`),迁移零成本。`ToolModule` 基类对应 abstract 也随之改为 `void Register(ModuleContext context)`。

### 4.2 `ModuleContext` —— 注册阶段的三个目标

```csharp
namespace DS.Tools.Core.Modularization;

/// <summary>模块在 Register 阶段可写入的三个聚合目标。</summary>
public sealed class ModuleContext
{
    public required IServiceCollection Services { get; init; }
    public required IViewRegistry Views { get; init; }          // 见文档 02
    public required INavigationRegistry Navigation { get; init; } // 见文档 04
}
```

聚合为单一对象的好处:未来扩展(如本地化资源注册)只改 `ModuleContext`,不动 `IToolModule` 签名。

### 4.3 模块目录 `IModuleCatalog`

```csharp
namespace DS.Tools.Core.Modularization;

public interface IModuleCatalog
{
    IReadOnlyList<ModuleInfo> Modules { get; }

    /// <param name="factory">编译期工厂委托,如 <c>() => new TextModule()</c>。</param>
    /// <param name="id">可选,缺省取模块自身 <c>Id</c>。</param>
    /// <param name="dependsOn">可选,依赖的其他模块 Id。</param>
    IModuleCatalog AddModule(Func<IToolModule> factory, string? id = null, params string[] dependsOn);
}

public sealed class ModuleInfo
{
    public required string Id { get; init; }
    public required Func<IToolModule> Factory { get; init; }
    public IReadOnlyList<string> DependsOn { get; init; } = [];
}
```

实现要点:`AddModule` **不立即实例化**模块(避免在目录构建期产生副作用),只存工厂;`Id` 可缺省,实例化后从 `module.Id` 取。

### 4.4 两阶段编排 `IModuleManager`

```csharp
namespace DS.Tools.Core.Modularization;

public interface IModuleManager
{
    /// <summary>阶段①:拓扑排序后逐模块实例化并调 <see cref="IToolModule.Register"/>。Build 前调用。</summary>
    void RegisterAll(IModuleCatalog catalog, ModuleContext context);

    /// <summary>阶段②:逐模块调 <see cref="IToolModule.Initialize"/>,并把模块登记进 <see cref="IToolRegistry"/>。Build 后调用。</summary>
    void InitializeAll(IModuleCatalog catalog, IServiceProvider provider, IToolRegistry toolRegistry);
}
```

实现内部维护一个 `List<IToolModule>`(阶段① 实例化后的缓存),供阶段② 复用,避免模块被实例化两次。

### 4.5 完整启动时序(`App.axaml.cs`)

```
OnFrameworkInitializationCompleted
┌─────────────────────────────────────────────────────────────┐
│ 1. BuildConfiguration() → IConfiguration                     │
│ 2. var services = new ServiceCollection()                    │
│    services.AddSingleton(configuration)                      │
│    services.AddLogging(...)                                  │
│    services.AddCoreServices()         // 含 Catalog/Manager/ │
│    services.AddApplicationServices()  // ViewRegistry/Nav... │
│                                                              │
│ 3. var catalog = new ModuleCatalog();                        │
│    catalog.AddModule(() => new TextModule());  // 编译期显式  │
│                                                              │
│ 4. var ctx = new ModuleContext {                             │
│        Services = services,                                  │
│        Views = viewRegistry,                                 │
│        Navigation = navRegistry                              │
│    };                                                        │
│    moduleManager.RegisterAll(catalog, ctx);   ─── 阶段①      │
│                                                              │
│ 5. var provider = services.BuildServiceProvider();           │
│                                                              │
│ 6. moduleManager.InitializeAll(catalog, provider,            │
│                                toolRegistry);   ─── 阶段②    │
│    // 内部:module.Initialize(provider);                      │
│    //        toolRegistry.Register(module);                  │
│                                                              │
│ 7. ApplyThemeSettings(...)                                   │
│ 8. DataTemplates.Add(new ViewLocator(viewRegistry));         │
│ 9. navService.NavigateTo(AppConstants.DefaultToolId);        │
│ 10. MainWindow + DataContext                                 │
└─────────────────────────────────────────────────────────────┘
```

## 5. `ModuleManager` 实现要点

```csharp
internal sealed class ModuleManager : IModuleManager
{
    private readonly List<IToolModule> _instances = [];
    private readonly IModuleDependencySorter _sorter;   // 可选,见 §6

    public void RegisterAll(IModuleCatalog catalog, ModuleContext context)
    {
        var ordered = _sorter.Sort(catalog.Modules);   // 拓扑序
        foreach (var info in ordered)
        {
            var module = info.Factory();               // 编译期 new,无反射
            module.Register(context);                   // 阶段①:只注册
            _instances.Add(module);
        }
    }

    public void InitializeAll(IModuleCatalog catalog, IServiceProvider provider, IToolRegistry toolRegistry)
    {
        foreach (var module in _instances)             // 已按依赖序排好
        {
            module.Initialize(provider);                // 阶段②:可解析服务
            toolRegistry.Register(module);              // 登记到运行期注册表
        }
    }
}
```

## 6. 依赖排序(可选)

单一 `TextModule` 无需排序,但若未来拆分为多模块,提供 `IModuleDependencySorter`:

```csharp
public interface IModuleDependencySorter
{
    /// <summary>Kahn 拓扑排序。缺失依赖抛 InvalidOperation;环抛 InvalidOperation。</summary>
    IReadOnlyList<ModuleInfo> Sort(IReadOnlyList<ModuleInfo> modules);
}
```

实现复用已被删除的旧 `ModuleDependencySorter` 的 Kahn 算法思路(纯逻辑,可单测,置于 `DS.Tools.Tests`)。当前可先提供「保序直通」实现,后续按需切换。

## 7. AOT / Trim 合规核对

- ✅ 模块实例化走 `info.Factory()`(`() => new TextModule()`),编译期已知,无 `Activator.CreateInstance`。
- ✅ 模块发现是主应用编译期 `catalog.AddModule(...)`,无程序集扫描。
- ✅ `Register`/`Initialize` 全程无 `(Type, Type)` 反射注册、无 `GetService(Type)` 解析。
- ✅ `ModuleContext` 持有的是 `IServiceCollection` / 接口,不持有 `Type` 表。
- ⚠️ `ModuleInfo.Factory` 是 `Func<IToolModule>`,Trim 分析器对委托捕获的 `new T()` 友好(构造器在编译期可见)。

## 8. 备选方案与权衡

| 方案 | 优点 | 缺点 | 取舍 |
|---|---|---|---|
| **两阶段 + Catalog(本设计)** | 模块可在 Build 前注册私有服务,符合 MS.DI 语义 | 启动流程略长 | ✅ 采用 |
| 单阶段(Build 后一次性 Register+Initialize) | 简单 | Build 后无法往容器加服务,模块私有服务无处注册 | ❌ |
| 反射扫描发现模块 | 零配置 | 直接违反 AOT,编译失败 | ❌ |
| 外部目录文件(JSON/ZIP)描述模块 | 可热插拔 | 需 `Assembly.LoadFrom`,违反 AOT | ❌ |

## 9. 从当前代码迁移步骤

1. 新建 `Cores/DS.Tools.Core/Modularization/` 目录,放入 `ModuleContext`、`IModuleCatalog`/`ModuleCatalog`、`ModuleInfo`、`IModuleManager`/`ModuleManager`、(可选)`IModuleDependencySorter`。
2. `IToolModule.Register` 签名改为 `void Register(ModuleContext context)`;`ToolModule` 基类同步。
3. `AddCoreServices` 注册 `IModuleCatalog`(singleton)、`IModuleManager`(singleton)、`IViewRegistry`、`INavigationRegistry`/`INavigationService`(见 02/04)。
4. 主应用 `DS.Tools.csproj` 增加 `<ProjectReference Include="..\Tools\DS.Tools.Module.Text\...">`。
5. `App.axaml.cs` 按第 4.5 节时序重写 `OnFrameworkInitializationCompleted`。
6. 清理 `App.axaml.cs` 类注释、`ServiceCollectionExtensions` 注释中残留的旧 Prism 描述(已过时)。

## 10. 验收标准

- [ ] 启动后 `IToolRegistry.Tools` 非空,包含 `TextModule` 一项。
- [ ] `MainWindowViewModel.Tools` 渲染出侧边栏项。
- [ ] 模块构造函数无 DI 参数,`Register` 内不调用任何 `GetService`。
- [ ] `dotnet build` 在 `TreatWarningsAsErrors=true` 下通过,无 IL2xxx 警告。
