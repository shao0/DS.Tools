using Xunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using DS.Tools.Core.DI;
using DS.Tools.Module.Base.DI;
using DS.Tools.Module.Text;

namespace DS.Tools.Tests.UnitTests;

/// <summary>
/// 容器集成测试 - 验证 App.axaml.cs 组合根的 DI 配置能解析所有工具 ViewModel
/// （等价于生产启动路径，防止"界面无法显示"类问题回归）
/// </summary>
public sealed class ToolModuleContainerTests
{
    [Fact]
    public void Container_ShouldResolveAllToolModuleViewModels()
    {
        // Arrange：复刻 App.ConfigureServices + RegisterToolModules 的组合根配置
        var services = new ServiceCollection();

        services.AddLogging(); // ILogger<T> 由标准 AddLogging 提供（生产为 AddSerilog）
        services.AddApplicationServices();
        services.AddModuleServices();

        var module = new TextModule();
        module.Register(services);
        services.AddSingleton(module);

        var sp = services.BuildServiceProvider();

        // Act & Assert：模块主 VM 与全部子工具 VM 均能解析
        module.CreateMainViewModel(sp).Should().NotBeNull();

        foreach (var subTool in module.SubTools!)
        {
            var viewModel = subTool.CreateViewModel(sp);
            viewModel.Should().NotBeNull($"子工具 '{subTool.Id}' 的 ViewModel 应能被 DI 解析");
        }
    }
}
