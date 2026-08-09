# 04 · 界面导航功能架构设计

> 关联文档:[01-模块加载方式](01-module-loading.md) · [02-View 与 ViewModel 注册](02-view-viewmodel-registration.md) · [03-模块 IoC 注册](03-module-ioc-registration.md) · [总览](README.md)
>
> 适用范围:`DS.Tools.Core/Interfaces` + `Services`、`DS.Tools/ViewModels/MainWindowViewModel.cs`、`MainWindow.axaml`

## 1. 背景与现状

当前导航链路三处断裂:

1. **无导航服务**:旧文档提及的 `INavigationService`/`NavigationService` 在 `Core` 中**不存在**(`Interfaces/` 只有 `IToolModule`/`IToolRegistry`/`IThemeService`/`ILocalizationService`/`IEventAggregator`)。
2. **`SelectToolCommand` 空体**(`MainWindowViewModel.cs:69`):
   ```csharp
   [RelayCommand]
   private void SelectTool(IToolModule? tool)
   {
       if (tool is not null)
       {
       }   // ← 空实现,什么都不做
   }
   ```
3. **`ActiveToolViewModel` 恒 null**(`MainWindowViewModel.cs:51`):`OnViewModelChanged` 从未被调用,`MainWindow.axaml:205` 的 `ContentControl Content={Binding ActiveToolViewModel}` 因此永远为空。

期望:点击侧边栏工具项 → 解析对应 VM → 设为当前 → `ContentControl` 经 [ViewLocator](02-view-viewmodel-registration.md) 渲染对应 View;启动时默认导航到 `dashboard`。

## 2. 目标

1. 提供 `INavigationService`:按 `toolId` 导航,持有 `Current` VM,通知订阅者。
2. 模块在 `Register` 阶段登记「toolId → VM 工厂」,工厂从 `IServiceProvider` 解析(支持依赖注入)。
3. 单区域导航(适配现有 `ContentControl`),不上来就做多 Region。
4. 全程标准 `event`,不引入 ReactiveUI。
5. 与 `IToolRegistry.ActiveTool` 协同:导航即更新选中态。

## 3. 设计约束(AOT / Trim 红线)

| ❌ 禁止 | ✅ 替代 |
|---|---|
| `provider.GetService(viewModelType)` 按 `Type` 解析 VM | 工厂委托 `Func<IServiceProvider, TViewModel>`,泛型 `GetRequiredService<T>()` |
| 反射 `Activator.CreateInstance(viewModelType)` | 预注册 VM 工厂 |
| ReactiveUI `IObservable`/`Subject` | 标准 `event Action<T>?` |
| 导航服务持有 `Type` 表对外暴露 | 内部 `Dictionary<string, Func<IServiceProvider, ViewModelBase>>` |

> `IToolModule.ViewModelType` 仅作元数据(分组/调试),**不参与运行时实例化**。VM 实例化唯一通道是导航工厂。

## 4. 设计

### 4.1 注册侧:`INavigationRegistry`

模块在阶段①(`Register`)写入「toolId → VM 工厂」:

```csharp
using DS.Tools.Core.Models;

namespace DS.Tools.Core.Modularization;

public interface INavigationRegistry
{
    /// <param name="factory">从已构建容器解析 VM 的委托,
    /// 如 <c>sp => sp.GetRequiredService&lt;DashboardViewModel&gt;()</c>。</param>
    void Register<TViewModel>(string toolId, Func<IServiceProvider, TViewModel> factory)
        where TViewModel : ViewModelBase;

    /// <summary>供导航服务在 Build 后固化(把 Func 换成绑定到具体 provider 的闭包)。</summary>
    IReadOnlyDictionary<string, Func<IServiceProvider, ViewModelBase>> Entries { get; }
}
```

实现:

```csharp
internal sealed class NavigationRegistry : INavigationRegistry
{
    private readonly Dictionary<string, Func<IServiceProvider, ViewModelBase>> _entries = new();

    public void Register<TViewModel>(string toolId, Func<IServiceProvider, TViewModel> factory)
        where TViewModel : ViewModelBase
    {
        ArgumentNullException.ThrowIfNull(factory);
        _entries[toolId] = factory;   // TViewModel 是 ViewModelBase,协变安全
    }

    public IReadOnlyDictionary<string, Func<IServiceProvider, ViewModelBase>> Entries => _entries;
}
```

> 模块通过 `ToolModule.RegisterTool<,>`(见 [03](03-module-ioc-registration.md) §4.4)自动调用 `context.Navigation.Register<TVm>(toolId, sp => sp.GetRequiredService<TVm>())`,无需手写。

### 4.2 运行时侧:`INavigationService`

```csharp
using Avalonia.Styling;  // 仅当需要与主题联动时;此处无
using DS.Tools.Core.Models;

namespace DS.Tools.Core.Interfaces;

public interface INavigationService
{
    /// <summary>当前活动 VM(供 MainWindowViewModel.ActiveToolViewModel 绑定)。</summary>
    ViewModelBase? Current { get; }

    /// <summary>导航到指定 toolId。成功返回 true;未注册返回 false。</summary>
    bool NavigateTo(string toolId);

    /// <summary>导航发生时触发(标准 .NET 事件,非 ReactiveUI)。</summary>
    event Action<ViewModelBase?>? Navigated;
}
```

实现(Build 后从 `INavigationRegistry` 与 `IServiceProvider` 构造):

```csharp
internal sealed class NavigationService : INavigationService
{
    private readonly IReadOnlyDictionary<string, Func<IServiceProvider, ViewModelBase>> _entries;
    private readonly IServiceProvider _provider;
    private readonly IToolRegistry _toolRegistry;

    public NavigationService(
        INavigationRegistry registry,
        IServiceProvider provider,
        IToolRegistry toolRegistry)
    {
        _entries = registry.Entries;
        _provider = provider;
        _toolRegistry = toolRegistry;
    }

    public ViewModelBase? Current { get; private set; }

    public bool NavigateTo(string toolId)
    {
        if (!_entries.TryGetValue(toolId, out var factory))
            return false;

        var vm = factory(_provider);        // 泛型 GetRequiredService<T>,AOT 安全
        Current = vm;
        _toolRegistry.ActiveTool = _toolRegistry.GetTool(toolId);  // 同步选中态
        Navigated?.Invoke(vm);
        return true;
    }

    public event Action<ViewModelBase?>? Navigated;
}
```

> `INavigationService` 注册为 **singleton**。由于它在 Build 后才需要"registry.entries + provider",可在 `BuildServiceProvider` 之后从容器解析(此时所有依赖已就绪)。

### 4.3 启动期默认导航(`App.axaml.cs`)

```csharp
// 在 moduleManager.InitializeAll 之后、创建 MainWindow 之前:
var navService = _serviceProvider.GetRequiredService<INavigationService>();
navService.NavigateTo(AppConstants.DefaultToolId);   // "dashboard"
```

### 4.4 `MainWindowViewModel` 接线

构造函数注入 `INavigationService`,订阅 `Navigated` 更新 `ActiveToolViewModel`:

```csharp
public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly IToolRegistry _toolRegistry;
    private readonly IThemeService _themeService;
    private readonly INavigationService _navigationService;

    public MainWindowViewModel(
        IToolRegistry toolRegistry,
        IThemeService themeService,
        INavigationService navigationService)
    {
        _toolRegistry = toolRegistry;
        _themeService = themeService;
        _navigationService = navigationService;

        Tools = new ObservableCollection<IToolModule>(_toolRegistry.Tools);

        _navigationService.Navigated += OnNavigated;     // 标准事件订阅

        // 若启动期 App.axaml.cs 已 NavigateTo("dashboard"),
        // 此处补一次同步(构造晚于导航时):
        ActiveToolViewModel = _navigationService.Current;

        UpdateThemeIcon();
    }

    public ObservableCollection<IToolModule> Tools { get; }

    [ObservableProperty] private ViewModelBase? _activeToolViewModel;
    [ObservableProperty] private string _currentThemeIcon = "🌙";

    private void OnNavigated(ViewModelBase? vm) => ActiveToolViewModel = vm;

    [RelayCommand]
    private void SelectTool(IToolModule? tool)
    {
        if (tool is null) return;
        _navigationService.NavigateTo(tool.Id);   // ← 真正的导航
    }

    [RelayCommand]
    private void ToggleTheme() { /* 不变 */ }

    private void UpdateThemeIcon() { /* 不变 */ }
}
```

## 5. 导航链路(端到端)

```
用户点击侧边栏 Button
  └─ Command={Binding SelectToolCommand}, CommandParameter={Binding}(IToolModule)
        │
        ▼
  MainWindowViewModel.SelectTool(tool)
        │  _navigationService.NavigateTo(tool.Id)
        ▼
  NavigationService.NavigateTo
        │  _entries[toolId].factory(_provider)
        │     └─ sp.GetRequiredService<XxxViewModel>()   (DI 解析依赖,AOT 安全)
        │  Current = vm
        │  _toolRegistry.ActiveTool = GetTool(toolId)     (同步侧边栏选中态)
        │  Navigated?.Invoke(vm)
        ▼
  MainWindowViewModel.OnNavigated(vm)
        │  ActiveToolViewModel = vm   ([ObservableProperty] 自动通知)
        ▼
  MainWindow.axaml: ContentControl Content={Binding ActiveToolViewModel}
        │
        ▼
  ViewLocator.Build(vm) → new XxxView()   (见文档 02)
        │
        ▼
  渲染 XxxView,x:DataType 编译绑定生效
```

## 6. ViewModel 生命周期策略

| 策略 | 实现 | 适用 | 取舍 |
|---|---|---|---|
| **Transient(每次新建)** | VM 注册为 `Transient`,工厂每次 `GetRequiredService` 返回新实例 | 默认;工具间切换不留状态 | ✅ 默认采用,简单、无泄漏 |
| 缓存(保留状态) | `NavigationService` 内 `Dictionary<string, ViewModelBase>` 缓存,首次创建后复用 | 用户期望切换工具时保留输入 | 可选增强;需处理释放(`IDisposable`) |
| Singleton(全局唯一) | VM 注册为 `Singleton` | 极少;需跨处共享同一 VM 状态 | 一般不推荐 |

> 当前 `RegisterTool`(文档 03)用 `AddTransient<TViewModel>()`,即 Transient 策略。若未来要"切回工具保留上次输入",改为缓存策略(只改 `NavigationService`,不动模块)。

## 7. 与 `IToolRegistry` 的协同

- `ToolRegistry.ActiveTool`(`ToolRegistry.cs:54`)是侧边栏选中态的来源,已有 `ToolChanged` 事件。
- `NavigateTo` 内 `_toolRegistry.ActiveTool = GetTool(toolId)` 让选中态跟随导航。
- 反向(外部改 `ActiveTool` 触发导航)当前**不**支持,避免双向触发循环;如需,可让侧边栏用 `ActiveTool` 显示高亮、用 `SelectToolCommand` 触发导航,二者单向。

## 8. 错误处理

| 场景 | 行为 |
|---|---|
| `NavigateTo` 的 `toolId` 未注册 | 返回 `false`,不抛异常;`Current` 不变。UI 可选择显示提示。 |
| VM 工厂解析依赖失败(`GetRequiredService` 抛) | 异常上抛到命令层;Avalonia 命令异常被吞,建议在 `NavigateTo` 内 `try/catch` 记录日志并回退。 |
| `toolRegistry.GetTool(toolId)` 返回 null | `ActiveTool` 设为 null,侧边栏不高亮,VM 仍正常渲染。 |

## 9. AOT / Trim 合规核对

- ✅ VM 实例化唯一通道是工厂 `Func<IServiceProvider, TViewModel>`,内部 `GetRequiredService<T>()` 泛型,无 `GetService(Type)`。
- ✅ `INavigationRegistry` 用 `string` 做 key,工厂委托为值,无 `Type` 表暴露。
- ✅ `Navigated` 是标准 `event Action<ViewModelBase?>?`,无 System.Reactive。
- ✅ `NavigateTo(string)` 入参为字符串,无 `Type`/反射。
- ⚠️ 泛型 `Register<TViewModel>` 在 AOT 下被特化,工厂闭包编译期可见。

## 10. 备选方案与权衡

| 方案 | 描述 | 取舍 |
|---|---|---|
| **单区域 + INavigationService(本设计)** | 一个 `Current`,适配现有 `ContentControl` | ✅ 采用;贴合"工具集一次显示一个工具"语义 |
| 多 Region(Prism `RegionManager`) | 多区域可同时显示多个 VM | ❌ 过度设计;UI 无多区域需求 |
| 直接在 VM 里 `new` 子 VM 并赋值 | 无服务,VM 自管 | ❌ VM 耦合容器;难测试、难扩展 |
| 事件总线触发导航 | 经 `IEventAggregator` 发导航事件 | 解耦但增加间接性;当前直连更清晰 |

## 11. 从当前代码迁移步骤

1. 新建 `Cores/DS.Tools.Core/Modularization/INavigationRegistry.cs` + `NavigationRegistry.cs`。
2. 新建 `Cores/DS.Tools.Core/Interfaces/INavigationService.cs` + `Services/NavigationService.cs`。
3. `AddCoreServices` 注册 `INavigationRegistry`(singleton);`NavigationService` 注册为 singleton(构造注入 registry/provider/toolRegistry)。
4. `ToolModule.RegisterTool<,>`(文档 03)内部调用 `context.Navigation.Register<TVm>(...)`。
5. 重写 `MainWindowViewModel.cs`(§4.4),补 `INavigationService` 依赖与 `OnNavigated`。
6. `App.axaml.cs` 在 `InitializeAll` 之后 `NavigateTo(AppConstants.DefaultToolId)`。
7. 单测:`DS.Tools.Tests` 注册一个 dummy 工具,断言 `NavigateTo` 后 `Current` 非空且 `Navigated` 触发一次。

## 12. 验收标准

- [ ] 启动后内容区显示 `DashboardView`(默认导航 `dashboard`)。
- [ ] 点击侧边栏任一工具项,内容区切换到对应 View。
- [ ] `IToolRegistry.ActiveTool` 跟随导航更新(侧边栏高亮)。
- [ ] 导航到不存在的 id 返回 `false`,不崩溃。
- [ ] `MainWindowViewModel` 可在无真实 `INavigationService` 的情况下用 Moq 构造(可测试性)。
- [ ] `dotnet build` 无 IL2xxx 警告。
