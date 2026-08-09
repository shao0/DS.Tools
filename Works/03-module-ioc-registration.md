# 03 · 模块自管 IoC 注册设计

> 关联文档:[01-模块加载方式](01-module-loading.md) · [02-View 与 ViewModel 注册](02-view-viewmodel-registration.md) · [04-导航功能架构](04-navigation-architecture.md) · [总览](README.md)
>
> 适用范围:`Cores/DS.Tools.Module.Base/ToolModule.cs`、各业务模块、`DS.Tools.Core/DI`

## 1. 背景与现状

`IToolModule` 已声明 `Register` 与 `Initialize` 两个生命周期方法,但:

- `DS.Tools.Module.Text.TextModule.Register` 直接 `return services;`(**空注册**),未登记任何私有服务或 ViewModel。
- 现有 `DS.Tools.Module.Text/Services/` 下已有 `IJsonFormatterService` + `JsonFormatterService` + `Models/JsonFormatterResult.cs`,**完全未被 IoC 容器收录**。
- `ToolModule` 基类(`Cores/DS.Tools.Module.Base/ToolModule.cs`)的 XML doc 注释仍提 `RegisterServices` / `OnInitializedCore` / `RegisterViewModel` / `IContainerProvider` 等**不存在的成员**,容易误导。本文档同时给出基类的目标结构。

期望:模块作为"自治单元",在 `Register` 阶段把自己的私有服务、ViewModel 全部登记进容器;`Initialize` 阶段按需解析做启动初始化。主应用不感知每个模块的具体服务。

## 2. 目标

1. 模块**独占管理**自身的私有服务与 VM 注册(主应用 `AddCoreServices`/`AddApplicationServices` 只管核心)。
2. 三类注册**同一阶段完成**(Build 前):私有服务、ViewModel、[View 映射](02-view-viewmodel-registration.md)、[导航条目](04-navigation-architecture.md)。
3. 严格遵守 AOT 红线:仅工厂委托 / 泛型重载。
4. `ToolModule` 基类用**模板方法**统一注册骨架,子类以最小代码声明意图。

## 3. 设计约束(AOT / Trim 红线)

| ❌ 禁止 | ✅ 替代 |
|---|---|
| `services.AddSingleton(typeof(IXxx), typeof(Xxx))` | `services.AddSingleton<IXxx>(_ => new Xxx())` 或 `AddSingleton<IXxx, Xxx>()` 泛型重载 |
| `provider.GetService(type)` 非泛型解析 | `provider.GetRequiredService<T>()` / `GetService<T>()` |
| 反射扫描程序集批量注册服务 | 模块 `Register` 内逐条显式声明 |
| 模块构造函数注入私有服务 | 构造无参;服务在 `Register` 登记,`Initialize` 或 VM 工厂内解析 |
| 手写 `new` 创建带依赖的服务实例(漏注册) | 走容器,由 DI 解析依赖链 |

## 4. 设计

### 4.1 `Register` 的三类职责(分工)

模块的 `Register(ModuleContext context)` 集中完成:

| 职责 | 写入目标 | 示例 |
|---|---|---|
| ① 私有服务 | `context.Services` | `AddSingleton<IJsonFormatterService>(_ => new JsonFormatterService())` |
| ② ViewModel | `context.Services` | `AddTransient<JsonFormatterViewModel>()` |
| ③ View 映射 | `context.Views` | `Views.Register<JsonFormatterViewModel>(() => new JsonFormatterView())` |
| ④ 导航条目 | `context.Navigation` | `Navigation.Register<JsonFormatterViewModel>("json-formatter", sp => sp.GetRequiredService<JsonFormatterViewModel>())` |

> ②③④ 通常**成对出现**(每个工具一组)。`ToolModule` 基类提供 `RegisterTool<TVm>(...)` 模板方法把这三步合一,见 §4.4。

### 4.2 私有服务的生命周期选择

| 服务类型 | 生命周期 | 理由 |
|---|---|---|
| 无状态算法/格式化器(`JsonFormatterService`) | **Singleton** | 纯计算,可共享 |
| ViewModel | **Transient** | 每次导航取新实例,避免跨工具状态串扰(保留状态策略见文档 04 §6) |
| 资源型(如缓存) | Singleton | 全局唯一 |
| 带会话状态的服务 | Scoped/Transient | 视语义而定 |

### 4.3 VM 的依赖注入:工厂委托 vs 泛型重载

VM 通常需要私有服务(如 `JsonFormatterViewModel` 依赖 `IJsonFormatterService`)。两种等价写法:

```csharp
// 写法 A:泛型重载(依赖链由 DI 自动解析,推荐)
services.AddTransient<JsonFormatterViewModel>();
// JsonFormatterViewModel 的构造参数 IJsonFormatterService 由容器注入

// 写法 B:显式工厂委托(当 VM 需要非 DI 参数时用)
services.AddTransient<JsonFormatterViewModel>(sp =>
    new JsonFormatterViewModel(sp.GetRequiredService<IJsonFormatterService>()));
```

> 写法 A 更简洁且与构造签名自动对齐;写法 B 适合 VM 需要运行时参数。二者都 AOT 安全。

### 4.4 `ToolModule` 基类目标结构(模板方法)

替换现有 `ToolModule.cs` 的过时 XML doc,落地为:

```csharp
using DS.Tools.Core.Interfaces;
using DS.Tools.Core.Modularization;
using DS.Tools.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DS.Tools.Module.Base;

/// <summary>
/// 工具模块抽象基类(实现 IToolModule)。
/// 子类通常只需实现元数据(Id/Name/Icon/Description/ViewModelType),
/// 并重写 Register/Initialize。便捷方法 RegisterTool 同时登记 VM/View/导航。
/// </summary>
public abstract class ToolModule : IToolModule
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Icon { get; }
    public abstract string Description { get; }
    public abstract Type ViewModelType { get; }

    /// <summary>阶段①:注册私有服务、VM、View 映射、导航条目。Build 前,禁止解析。</summary>
    public abstract void Register(ModuleContext context);

    /// <summary>阶段②:从已构建容器解析服务做初始化。默认空实现。</summary>
    public virtual void Initialize(IServiceProvider services) { }

    /// <summary>便捷:一次性登记一个工具的 VM(IoC)+ View 映射 + 导航条目。</summary>
    protected static void RegisterTool<TViewModel, TView>(
        ModuleContext context, string toolId)
        where TViewModel : ViewModelBase
        where TView : Avalonia.Controls.Control, new()
    {
        // ①② VM(IoC,Transient,依赖由 DI 注入)
        context.Services.AddTransient<TViewModel>();
        // ③ View 映射(工厂委托)
        context.Views.Register<TViewModel>(() => new TView());
        // ④ 导航条目(从容器解析 VM)
        context.Navigation.Register<TViewModel>(toolId, sp => sp.GetRequiredService<TViewModel>());
    }
}
```

### 4.5 完整示例:`TextModule`

`TextModule` 内含 7 个工具,用 `RegisterTool` 批量登记:

```csharp
using DS.Tools.Module.Base;
using DS.Tools.Module.Text.Services;
using DS.Tools.Module.Text.ViewModels;
using DS.Tools.Module.Text.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DS.Tools.Module.Text;

public sealed class TextModule : ToolModule
{
    public override string Id => "text";
    public override string Name => "文本工具";
    public override string Icon => "📝";
    public override string Description => "文本相关工具集合";
    public override Type ViewModelType => typeof(DashboardViewModel);

    public override void Register(ModuleContext context)
    {
        // —— 私有服务(JsonFormatter 专用)——
        context.Services.AddSingleton<IJsonFormatterService>(_ => new JsonFormatterService());

        // —— 7 个工具:VM(IoC)+ View 映射 + 导航条目 ——
        RegisterTool<DashboardViewModel,          DashboardView          >(context, "dashboard");
        RegisterTool<JsonFormatterViewModel,      JsonFormatterView      >(context, "json-formatter");
        RegisterTool<Base64ViewModel,             Base64View             >(context, "base64");
        RegisterTool<TimestampConverterViewModel, TimestampConverterView >(context, "timestamp-converter");
        RegisterTool<ColorConverterViewModel,     ColorConverterView     >(context, "color-converter");
        RegisterTool<PasswordGeneratorViewModel,  PasswordGeneratorView  >(context, "password-generator");
        RegisterTool<TextHasherViewModel,         TextHasherView         >(context, "text-hash");
    }

    public override void Initialize(IServiceProvider services)
    {
        // 若需要启动期校验/预热,在此解析:
        // _ = services.GetRequiredService<IJsonFormatterService>();
    }
}
```

> `toolId` 与 `appsettings.json:Tools.EnabledTools` 列表、`AppConstants.DefaultToolId="dashboard"` 对齐,确保导航 ID 一致。

## 5. 与 `AddCoreServices` / `AddApplicationServices` 的边界

| 注册方 | 位置 | 注册内容 |
|---|---|---|
| **核心** | `Core/DI/ServiceCollectionExtensions.AddCoreServices` | `ILoggerFactory`/`SerilogLoggerFactory`、`IEventAggregator`、`IModuleCatalog`、`IModuleManager`、`IViewRegistry`、`INavigationRegistry`、`INavigationService` |
| **核心** | `AddApplicationServices` | `IThemeService`、`ILocalizationService`、`IToolRegistry`、`AppConfigManager` |
| **模块** | `XxxModule.Register` | 模块私有服务 + VM + View 映射 + 导航条目 |

调用顺序(见 [01](01-module-loading.md) §4.5):`AddCoreServices` → `AddApplicationServices` → 建 `catalog` → `moduleManager.RegisterAll`(模块 Register,仍 Build 前)→ `BuildServiceProvider`。

## 6. AOT / Trim 合规核对

- ✅ 私有服务一律 `AddSingleton<TService>(_ => new TImpl())` 或 `AddSingleton<TService, TImpl>()`,无 `(Type, Type)`。
- ✅ VM 解析全走 `GetRequiredService<T>()` 泛型。
- ✅ `RegisterTool<TVm, TView>` 泛型约束 + `new()`,工厂 `() => new TView()` 编译期可见。
- ✅ 导航工厂 `Func<IServiceProvider, TViewModel>`,闭包捕获无 `Type` 表。
- ⚠️ `ViewModelType` 属性返回 `typeof(...)`,仅作元数据(如分组、调试),不用于实例化。

## 7. 备选方案与权衡

| 方案 | 描述 | 取舍 |
|---|---|---|
| **模块自治注册(本设计)** | 每模块在 `Register` 登记自己的服务 | ✅ 采用;主应用零感知 |
| 主应用集中注册所有服务 | 在 `AddApplicationServices` 写死所有 VM/服务 | ❌ 每加工具改 Core;违背模块化 |
| `IServiceCollection` 上挂"模块清单"扫描 | 反射扫描 `IToolModule` 实现并自动注册 | ❌ 违反 AOT |
| 工厂委托 vs 泛型重载 | 二者均可用 | 默认泛型重载;需运行时参数时用工厂 |

## 8. 从当前代码迁移步骤

1. 重写 `Cores/DS.Tools.Module.Base/ToolModule.cs`:删除过时 XML doc,改为 §4.4 的目标结构,新增 `RegisterTool<,>` 便捷方法。
2. `IToolModule.Register` 签名改为 `void Register(ModuleContext context)`(配合 [01](01-module-loading.md))。
3. 把 `TextModule.cs` 改写为 §4.5 的实现(补齐 7 个工具的注册)。
4. `App.axaml.cs` 在 `moduleManager.RegisterAll` 调用前确保 `ModuleContext` 三个目标均已由 `AddCoreServices` 注入容器并实例化。
5. 清理 `ServiceCollectionExtensions` 注释里关于 `AddModularization` 的过时描述。

## 9. 验收标准

- [ ] `provider.GetRequiredService<IJsonFormatterService>()` 可解析到单例实例。
- [ ] `provider.GetRequiredService<JsonFormatterViewModel>()` 可解析,且其 `IJsonFormatterService` 依赖被注入。
- [ ] 模块构造函数无 DI 参数(仅无参默认构造)。
- [ ] `Register` 内不出现任何 `GetService` 调用(只注册不解析)。
- [ ] `dotnet build` 无 IL2xxx 警告。
