# DS.Tools · 模块化与导航架构设计(总览)

> 本目录是为「**让当前半成品代码长出完整模块化 + 导航能力**」而写的目标架构设计文档,共 4 篇 + 本总览。
>
> 文档定位:**目标架构设计**(指导后续重构的蓝图),非最小补丁。每篇含「从当前代码迁移步骤」章节,可按统一路线图(见下)渐进落地。
>
> 编写日期:2026-07-26 · 基于 `DS.Tools.slnx`(6 项目)实际代码核对

## 为什么需要这套设计

代码库在 2026-07 的重构中删除了早期 Prism 风格模块化系统,但新骨架的**接线尚未完成**,导致应用可启动但功能不通:

| 断点 | 现象 | 由哪篇设计解决 |
|---|---|---|
| 模块未加载 | `App.axaml.cs` 不注册任何模块,`IToolRegistry.Tools` 为空,侧边栏空 | [01 模块加载方式](01-module-loading.md) |
| 无 View 渲染 | 旧 `ModuleViewLocator` 已删且无替代,`ContentControl` 显示 `ToString()` | [02 View 与 ViewModel 注册](02-view-viewmodel-registration.md) |
| IoC 未注册 | `TextModule.Register` 空实现,`IJsonFormatterService` 等孤立 | [03 模块 IoC 注册](03-module-ioc-registration.md) |
| 无导航 | `SelectToolCommand` 空体,`ActiveToolViewModel` 恒 null | [04 导航功能架构](04-navigation-architecture.md) |

## 文档导航

| # | 文档 | 核心产出 |
|---|---|---|
| 01 | [模块加载方式](01-module-loading.md) | `IModuleCatalog` / `ModuleInfo` / `IModuleManager` / `ModuleContext` / 两阶段生命周期 |
| 02 | [View 与 ViewModel 注册](02-view-viewmodel-registration.md) | `IViewRegistry` / `ViewLocator(IDataTemplate)` / 渲染链路 |
| 03 | [模块自管 IoC 注册](03-module-ioc-registration.md) | `ToolModule` 目标结构 / `RegisterTool<,>` / 私有服务 + VM 注册 |
| 04 | [导航功能架构](04-navigation-architecture.md) | `INavigationRegistry` / `INavigationService` / 导航链路 / VM 生命周期 |

## 统一目标架构(端到端)

```
                          ┌─────────────────────────────────────┐
   编译期(主应用)        │  App.axaml.cs                       │
                          │  catalog.AddModule(() => new        │
                          │     TextModule())                   │
                          └───────────────┬─────────────────────┘
                                          │ 阶段① RegisterAll (Build 前)
                 ┌────────────────────────┼────────────────────────┐
                 ▼                        ▼                        ▼
        IServiceCollection        IViewRegistry          INavigationRegistry
        · 私有服务(Singleton)    · VM→View 工厂         · toolId→VM 工厂
        · VM(Transient)          () => new XxxView()    sp => GetRequiredService<TVm>()
                 │                        │                        │
                 └────────────────────────┼────────────────────────┘
                                          │
                       services.BuildServiceProvider()   ──── 阶段分界
                                          │
                                          ▼  阶段② InitializeAll (Build 后)
                          ┌───────────────────────────────────────┐
                          │ module.Initialize(provider)            │
                          │ toolRegistry.Register(module)  → Tools │
                          └───────────────┬───────────────────────┘
                                          ▼
            INavigationService.NavigateTo("dashboard")  (默认导航)
                                          │
              ┌───────────────────────────┴───────────────────────┐
              ▼                                                       ▼
   MainWindowViewModel.ActiveToolViewModel                IToolRegistry.ActiveTool
   (CT.Mvvm [ObservableProperty] 通知)                     (侧边栏选中态)
              │
              ▼
   ContentControl Content={Binding ActiveToolViewModel}
              │
              ▼  Application.DataTemplates
   ViewLocator.Build(vm) → IViewRegistry.ResolveFor(vm) → new XxxView()
              │
              ▼
   渲染 XxxView(x:DataType 编译绑定生效)
```

## 统一契约清单(4 文档共用)

> 命名空间约定:`DS.Tools.Core.Modularization`(基础设施实现)、`DS.Tools.Core.Interfaces`(对外契约)。

```csharp
// ===== 生命周期 =====
namespace DS.Tools.Core.Interfaces;

public interface IToolModule                          // 已存在,签名演进
{
    string Id { get; } string Name { get; } string Icon { get; }
    string Description { get; } Type ViewModelType { get; }
    void Register(ModuleContext context);            // ① Build 前
    void Initialize(IServiceProvider services);      // ② Build 后
}

// ===== 模块化基础设施 =====
namespace DS.Tools.Core.Modularization;

public sealed class ModuleContext
{
    public required IServiceCollection Services { get; init; }
    public required IViewRegistry Views { get; init; }
    public required INavigationRegistry Navigation { get; init; }
}

public interface IModuleCatalog
{
    IReadOnlyList<ModuleInfo> Modules { get; }
    IModuleCatalog AddModule(Func<IToolModule> factory, string? id = null, params string[] dependsOn);
}
public sealed class ModuleInfo
{
    public required string Id { get; init; }
    public required Func<IToolModule> Factory { get; init; }
    public IReadOnlyList<string> DependsOn { get; init; } = [];
}
public interface IModuleManager
{
    void RegisterAll(IModuleCatalog catalog, ModuleContext context);
    void InitializeAll(IModuleCatalog catalog, IServiceProvider provider, IToolRegistry toolRegistry);
}
public interface IModuleDependencySorter                       // 可选,见 01 §6
{
    IReadOnlyList<ModuleInfo> Sort(IReadOnlyList<ModuleInfo> modules);
}

// ===== View 注册(02) =====
public interface IViewRegistry
{
    void Register<TViewModel>(Func<Control> viewFactory) where TViewModel : ViewModelBase;
    Control? ResolveFor(ViewModelBase viewModel);
    bool IsRegistered<TViewModel>() where TViewModel : ViewModelBase;
}

// ===== 导航注册(04) =====
public interface INavigationRegistry
{
    void Register<TViewModel>(string toolId, Func<IServiceProvider, TViewModel> factory) where TViewModel : ViewModelBase;
    IReadOnlyDictionary<string, Func<IServiceProvider, ViewModelBase>> Entries { get; }
}

// ===== 导航运行时(04) =====
namespace DS.Tools.Core.Interfaces;
public interface INavigationService
{
    ViewModelBase? Current { get; }
    bool NavigateTo(string toolId);
    event Action<ViewModelBase?>? Navigated;
}
```

## `App.axaml.cs` 目标启动流程(完整)

```
OnFrameworkInitializationCompleted
 1. BuildConfiguration() → IConfiguration
 2. var services = new ServiceCollection();
       services.AddSingleton(configuration);
       services.AddLogging(b => b.SetMinimumLevel(Information));
       services.AddCoreServices();        // +Catalog/Manager/ViewRegistry/NavRegistry/NavService
       services.AddApplicationServices(); // Theme/Localization/ToolRegistry/AppConfigManager
 3. var catalog = new ModuleCatalog();
       catalog.AddModule(() => new TextModule());          // 编译期显式
 4. var ctx = new ModuleContext { Services, Views, Navigation };
       moduleManager.RegisterAll(catalog, ctx);            // ── 阶段①
 5. _serviceProvider = services.BuildServiceProvider();
 6. moduleManager.InitializeAll(catalog, _serviceProvider, toolRegistry); // ── 阶段②
       → module.Initialize(provider); toolRegistry.Register(module);
 7. ApplyThemeSettings(...);
 8. DataTemplates.Add(new ViewLocator(viewRegistry));      // View 渲染接线
 9. navService.NavigateTo(AppConstants.DefaultToolId);     // 默认导航 dashboard
10. MainWindow + DataContext = MainWindowViewModel;
```

## 关键决策推荐一览

| 决策点 | 推荐方案 | 备选 | 理由 |
|---|---|---|---|
| 模块发现 | 编译期 `catalog.AddModule(factory)` | 反射扫描 | AOT 红线 |
| 模块生命周期 | 两阶段(Register@Build前 / Initialize@Build后) | 单阶段 | 私有服务需 Build 前注册 |
| View 映射 | `IViewRegistry` 工厂字典 + `ViewLocator` | AXAML `DataTemplate` 枚举 | 模块自治 |
| VM 实例化 | 导航工厂 `Func<IServiceProvider, TVm>` | `Activator.CreateInstance(type)` | AOT 红线 |
| 导航模型 | 单区域 `Current` + `ContentControl` | 多 Region | 贴合工具集语义 |
| VM 生命周期 | Transient(每次新建) | 缓存/Singleton | 简单、无状态串扰 |
| 事件机制 | 标准 `event Action<T>?` | ReactiveUI `IObservable` | AOT 红线 + 已移除 Rx |
| `Register` 签名 | `void Register(ModuleContext)` | `IServiceCollection Register(IServiceCollection)` | 聚合三目标,可扩展 |
| 依赖排序 | 暂用保序直通,预留 Kahn | 立即上 Kahn | 单模块用不上 |

## AOT / Trim 红线(全设计通用)

| ❌ 禁止 | ✅ 替代 |
|---|---|
| `Activator.CreateInstance` / `Type.GetType` | 工厂委托 `() => new T()` |
| `Assembly.LoadFrom` / 反射扫描 | 编译期 `catalog.AddModule(...)` |
| `AddSingleton(Type, Type)` 反射重载 | 泛型 / 工厂委托重载 |
| `GetService(Type)` / `Resolve(Type)` 非泛型 | `GetRequiredService<T>()` |
| 命名约定反射匹配 VM↔View | 显式 `IViewRegistry.Register` |
| 手写反射 JSON | `AppJsonContext` 源生成器 |
| ReactiveUI / System.Reactive | 标准 `event` |

> `viewModel.GetType()` 查 `Dictionary<Type, Func<Control>>` **允许**(非反射实例化)。真正禁止的是「拿 `Type` 后 `Activator.CreateInstance`」。

## 统一迁移路线图

建议按依赖顺序分 5 步落地(每步可独立编译/运行验证):

1. **基础设施骨架**([01](01-module-loading.md))
   - 新建 `Core/Modularization/`:`ModuleContext` / `IModuleCatalog`+`ModuleCatalog` / `ModuleInfo` / `IModuleManager`+`ModuleManager`(先用保序排序器)
   - `IToolModule.Register` 签名改为 `void Register(ModuleContext)`
   - `AddCoreServices` 注册上述 + `IViewRegistry` + `INavigationRegistry` + `INavigationService`

2. **View 注册**([02](02-view-viewmodel-registration.md))
   - `IViewRegistry`/`ViewRegistry`/`ViewLocator`
   - `App.axaml.cs`:`DataTemplates.Add(new ViewLocator(viewRegistry))`

3. **导航服务**([04](04-navigation-architecture.md))
   - `INavigationRegistry`/`NavigationRegistry`、`INavigationService`/`NavigationService`
   - 重写 `MainWindowViewModel`(注入 `INavigationService`,`OnNavigated` 更新 `ActiveToolViewModel`,`SelectTool` 调 `NavigateTo`)

4. **模块基类 + TextModule**([03](03-module-ioc-registration.md))
   - 重写 `ToolModule.cs`(目标结构 + `RegisterTool<,>`)
   - 重写 `TextModule.cs`(7 个工具批量 `RegisterTool` + 私有服务 `IJsonFormatterService`)
   - 主应用 `DS.Tools.csproj` 增加 `<ProjectReference>` 指向 `Module.Text`

5. **主应用接线 + 默认导航**
   - `App.axaml.cs` 按目标启动流程重写(`RegisterAll` → Build → `InitializeAll` → `DataTemplates` → `NavigateTo("dashboard")`)
   - 清理过时代码注释(`App.axaml.cs` 类注释 / `ServiceCollectionExtensions` / `ToolModule` XML doc)
   - (可选)更新 `README.md` 对齐新架构

每步完成后跑 `dotnet build DS.Tools.slnx` 确认 `TreatWarningsAsErrors` 下无 IL2xxx 警告;第 5 步完成后 `dotnet run` 验证侧边栏与默认 View 渲染。

## 术语表

| 术语 | 含义 |
|---|---|
| 阶段① / Register | `BuildServiceProvider` 之前,只注册不解析 |
| 阶段② / Initialize | `BuildServiceProvider` 之后,可解析服务 |
| `ModuleContext` | 模块 Register 阶段可写入的三目标聚合(Services/Views/Navigation) |
| 工厂委托 | 编译期已知的 `() => new T()` / `sp => sp.GetRequiredService<T>()`,AOT 安全 |
| 工具(`IToolModule`) | 一个可导航的业务单元,同时承载模块生命周期 |
| 单区域导航 | 同一时刻只显示一个 VM 的 `ContentControl` 模型 |

---

> 设计遵循 `CLAUDE.md` 中的 AOT/Trim 红线、CommunityToolkit.Mvvm 源生成器模式与中央包管理约定。落地前请以实际源码为准核对接口签名(本文档基于 2026-07-26 代码状态)。
