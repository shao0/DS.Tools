# 02 · View 与 ViewModel 对应注册设计

> 关联文档:[01-模块加载方式](01-module-loading.md) · [03-模块 IoC 注册](03-module-ioc-registration.md) · [04-导航功能架构](04-navigation-architecture.md) · [总览](README.md)
>
> 适用范围:`Cores/DS.Tools.Core/Modularization`、`DS.Tools.Views`、各业务模块 `Views/`

## 1. 背景与现状

`MainWindow.axaml:204` 用一个 `ContentControl` 显示当前工具视图:

```xml
<Border Background="#0F172A">
    <ContentControl Content="{Binding ActiveToolViewModel}" />
</Border>
```

但渲染链路在两个环节断裂:

1. **没有 VM → View 的映射**:旧的 `ModuleViewLocator`(`IDataTemplate`)已删除且无替代。`Application.DataTemplates` 为空,Avalonia 不知道某个 `ViewModelBase` 子类该渲染成哪个 `UserControl`,于是 `ContentControl` 只会显示 `ToString()`。
2. **`ActiveToolViewModel` 恒为 `null`**:`MainWindowViewModel.SelectToolCommand` 方法体为空(`if (tool is not null) {}`),`OnViewModelChanged` 从未被调用。(此问题在 [文档 04](04-navigation-architecture.md) 解决;本文聚焦映射与渲染。)

现有资产:`DS.Tools.Module.Text/Views/` 下已有 7 个完整 `UserControl`(`DashboardView` / `Base64View` / `ColorConverterView` / `JsonFormatterView` / `PasswordGeneratorView` / `TextHasherView` / `TimestampConverterView`),每个 `View.axaml` 都设了 `x:DataType` 指向对应 ViewModel,编译绑定已就绪。

## 2. 目标

1. 模块**自治**:每个模块在 `Register` 阶段声明自己「VM 类型 → View 工厂」的映射,不污染主应用。
2. `MainWindow` 的 `ContentControl` 收到任意 `ViewModelBase` 都能自动渲染对应 View。
3. **AOT 合规**:View 实例化走编译期工厂委托,零反射。
4. 编译绑定不被破坏(View 的 `x:DataType` 仍校验)。

## 3. 设计约束

| ❌ 禁止 | ✅ 替代 |
|---|---|
| `Activator.CreateInstance(viewType)` | 预注册工厂 `Func<Control>` = `() => new XxxView()` |
| 按命名约定反射匹配 VM↔View | 模块显式 `Register<TViewModel>(factory)` |
| `DataTemplate` 里写 `{x:Type}` 全部枚举(散落难维护) | 集中到 `IViewRegistry`,模块各自注册 |
| `ResolveFor` 接收 `Type` 参数对外暴露 | 接收 `ViewModelBase` 实例,内部 `GetType()` 查字典 |

> 说明:`viewModel.GetType()` 在 AOT 下安全(每对象都有类型句柄,非反射实例化);`Dictionary<Type, Func<Control>>.TryGetValue` 是纯查表,不触发 Trim 警告。真正禁止的是「拿到 Type 后去 `Activator.CreateInstance`」。

## 4. 设计

### 4.1 `IViewRegistry`

```csharp
using Avalonia.Controls;
using DS.Tools.Core.Models;

namespace DS.Tools.Core.Modularization;

public interface IViewRegistry
{
    /// <summary>注册"VM 类型 → View 工厂"。工厂必须无副作用、无参(如 <c>() => new DashboardView()</c>)。</summary>
    void Register<TViewModel>(Func<Control> viewFactory) where TViewModel : ViewModelBase;

    /// <summary>按 VM 实例的类型查 View 工厂并构造。未注册返回 null。</summary>
    Control? ResolveFor(ViewModelBase viewModel);

    /// <summary>是否注册过指定 VM 类型(供调试/断言)。</summary>
    bool IsRegistered<TViewModel>() where TViewModel : ViewModelBase;
}
```

实现:

```csharp
internal sealed class ViewRegistry : IViewRegistry
{
    private readonly Dictionary<Type, Func<Control>> _map = new();

    public void Register<TViewModel>(Func<Control> viewFactory) where TViewModel : ViewModelBase
    {
        ArgumentNullException.ThrowIfNull(viewFactory);
        _map[typeof(TViewModel)] = viewFactory;   // 覆盖重复注册(可选:抛异常)
    }

    public Control? ResolveFor(ViewModelBase viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        return _map.TryGetValue(viewModel.GetType(), out var factory) ? factory() : null;
    }

    public bool IsRegistered<TViewModel>() where TViewModel : ViewModelBase
        => _map.ContainsKey(typeof(TViewModel));
}
```

### 4.2 `ViewLocator` —— Avalonia `IDataTemplate` 适配器

```csharp
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using DS.Tools.Core.Models;

namespace DS.Tools.Core.Modularization;

/// <summary>
/// 把 ViewModelBase 解析为对应 View。注册到 Application.DataTemplates 后,
/// 所有 ContentControl 收到 VM 时自动调用 Build。
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    private readonly IViewRegistry _registry;

    public ViewLocator(IViewRegistry registry) => _registry = registry;

    /// <summary>仅处理 ViewModelBase 派生类型。</summary>
    public bool SupportsRecycling => false;

    public Control? Build(object? param)
    {
        if (param is not ViewModelBase vm)
            return null;   // 交给下一个 DataTemplate 或显示 null

        var view = _registry.ResolveFor(vm);
        if (view is null)
            return new TextBlock { Text = $"⚠ 未注册 View:{vm.GetType().Name}" };

        // 确保数据上下文同步(ContentControl 通常已设,显式兜底)
        if (ReferenceEquals(view.DataContext, null))
            view.DataContext = vm;
        return view;
    }

    public bool Match(object? data) => data is ViewModelBase;
}
```

> Avalonia 12 的 `IDataTemplate` 要求实现 `Build(object?)` 与 `Match(object?)`(部分版本含 `SupportsRecycling` 属性)。以实际 SDK 补全成员。

### 4.3 模块侧注册(`ToolModule` 子类)

以 `TextModule` 为例,在阶段① 的 `Register` 里集中登记全部 7 个工具的 View 映射:

```csharp
public override void Register(ModuleContext context)
{
    // —— 私有服务 + VM 的 IoC 注册见文档 03 ——
    // —— 导航条目见文档 04 ——

    // View 映射:工厂委托,AOT 安全
    context.Views.Register<DashboardViewModel>(() => new DashboardView());
    context.Views.Register<JsonFormatterViewModel>(() => new JsonFormatterView());
    context.Views.Register<Base64ViewModel>(() => new Base64View());
    context.Views.Register<TimestampConverterViewModel>(() => new TimestampConverterView());
    context.Views.Register<ColorConverterViewModel>(() => new ColorConverterView());
    context.Views.Register<PasswordGeneratorViewModel>(() => new PasswordGeneratorView());
    context.Views.Register<TextHasherViewModel>(() => new TextHasherView());
}
```

### 4.4 主应用接线(`App.axaml.cs`)

`IViewRegistry` 注册为 singleton(`AddCoreServices`),`ViewLocator` 在框架初始化后挂到 `DataTemplates`:

```csharp
var viewRegistry = _serviceProvider.GetRequiredService<IViewRegistry>();
DataTemplates.Add(new ViewLocator(viewRegistry));   // 全局生效
```

`MainWindow.axaml` **无需改动**——`ContentControl Content={Binding ActiveToolViewModel}` 收到 VM 后,`ViewLocator.Build` 自动返回对应 View。

## 5. 渲染链路(端到端)

```
[导航服务设 ActiveToolViewModel = vm]   (文档 04)
            │
            ▼  属性变更通知 (CT.Mvvm 源生成器)
   MainWindow ContentControl.Content = vm
            │
            ▼  Avalonia 遍历 Application.DataTemplates
        ViewLocator.Match(vm) = true   (vm is ViewModelBase)
            │
            ▼  ViewLocator.Build(vm)
   _viewRegistry.ResolveFor(vm)
            │
            ▼  viewModel.GetType() 查 Dictionary<Type, Func<Control>>
        factory()  →  new XxxView()
            │
            ▼  view.DataContext = vm
   ContentControl 渲染 XxxView,编译绑定 (x:DataType) 生效
```

## 6. 备选方案与权衡

| 方案 | 机制 | AOT | 维护性 | 取舍 |
|---|---|---|---|---|
| **IViewRegistry + ViewLocator(本设计)** | 模块注册工厂,字典查表 | ✅ | 模块自治,集中渲染 | ✅ 采用 |
| `App.axaml` 内 `<DataTemplate DataType>` 枚举 | AXAML 静态声明 | ✅ | 每加一工具改主应用 XAML;耦合主应用 | ❌ 违背模块自治 |
| 命名约定 + 反射(`XxxViewModel` → `XxxView`) | 字符串拼类型名 + `Activator.CreateInstance` | ❌ | 零注册 | ❌ 违反 AOT |
| `DataTemplate` + `x:CompileBindings` 约定 | 编译期匹配 | ✅ | 仅适合 VM 数量极少 | 仅小规模可接受 |

本设计本质是**已删除的旧 `ModuleViewLocator` 的精简复刻**:去掉对 `IContainerProvider`/Prism 容器的依赖,只保留「工厂字典 + IDataTemplate」最小核心。

## 7. AOT / Trim 合规核对

- ✅ `Register<TViewModel>(Func<Control>)` 的工厂 `() => new XxxView()` 构造器编译期可见。
- ✅ `ResolveFor` 用 `viewModel.GetType()` 查表,无 `Activator.CreateInstance`、无 `Type.GetType(string)`。
- ✅ `ViewLocator` 无反射,`IDataTemplate.Build` 接收 `object` 不接收 `Type`。
- ✅ View 的 `DataContext` 设为具体 VM 实例,`x:DataType` 编译绑定照常校验。
- ⚠️ `Dictionary<Type, Func<Control>>` 中 `typeof(TViewModel)` 在泛型方法内被 JIT/AOT 特化,无 trim 风险。

## 8. 从当前代码迁移步骤

1. 新建 `Cores/DS.Tools.Core/Modularization/IViewRegistry.cs` + `ViewRegistry.cs` + `ViewLocator.cs`。
2. `AddCoreServices` 注册 `IViewRegistry` 为 singleton。
3. 在 `TextModule.Register` 中补齐 7 条 `context.Views.Register<...>(...)`(配合 03/04 一起补)。
4. `App.axaml.cs` 框架初始化后 `DataTemplates.Add(new ViewLocator(viewRegistry))`。
5. 给 `MainWindow.axaml` 的 `ContentControl` 增加 `DataTemplates` 失败兜底提示(可选,见 `ViewLocator.Build` 的 `TextBlock`)。
6. 单测:在 `DS.Tools.Tests` 注册一个 dummy VM→View,断言 `ResolveFor` 返回正确类型(测试项目已关闭 AOT,可放心测)。

## 9. 验收标准

- [ ] `ContentControl` 收到任一已注册 VM 时,渲染对应 View 而非 `ToString()`。
- [ ] 未注册 VM 显示兜底提示,不抛异常。
- [ ] View 的 `x:DataType` 编译绑定在校验期仍生效(改 VM 属性名会编译失败)。
- [ ] `dotnet build` 无 IL2xxx 警告。
