using System.ComponentModel;
using NewLife.Agent;
using NewLife.Agent.Command;
using NewLife.Agent.WebPanel;

namespace UnitTest;

/// <summary>扩展点单元测试</summary>
/// <remarks>覆盖自定义命令发现、Web 面板工厂方法、嵌入式资源替换三个对外扩展点；会创建面板实例，串行执行</remarks>
[Collection("WebPanel")]
public class ExtensibilityTests
{
    #region 自定义命令扩展
    /// <summary>测试用自定义命令处理器，由 CommandFactory 反射发现</summary>
    public class UnitTestCommandHandler : BaseCommandHandler
    {
        /// <summary>实例化</summary>
        /// <param name="service">服务对象</param>
        public UnitTestCommandHandler(ServiceBase service) : base(service)
        {
            Cmd = "-unittestcustom";
            Description = "单元测试自定义命令";
        }

        /// <summary>是否已被调用</summary>
        public static Boolean Processed;

        /// <inheritdoc/>
        public override void Process(String[] args) => Processed = true;
    }

    [Fact]
    [DisplayName("自定义命令_反射发现_可被调用")]
    public void CustomCommand_DiscoveredAndInvoked()
    {
        var factory = new CommandFactory(new ServiceBaseTester(), typeof(UnitTestCommandHandler).Assembly);

        Assert.Contains(factory.GetAllCommand(), e => e.Cmd == "-unittestcustom");

        UnitTestCommandHandler.Processed = false;
        Assert.True(factory.Handle("-unittestcustom", null));
        Assert.True(UnitTestCommandHandler.Processed);
    }

    [Fact]
    [DisplayName("自定义命令_未定义快捷键_不进快捷菜单")]
    public void CustomCommand_NoShortcut_NotInMenu()
    {
        var svc = new ServiceBaseTester { ServiceName = "UnitTestMenu" };
        // 快捷菜单构建需访问宿主（判断服务是否已安装）
        svc.Host = new DefaultHost { Service = svc };

        var factory = new CommandFactory(svc, typeof(UnitTestCommandHandler).Assembly);

        Assert.DoesNotContain(factory.GetShortcutMenu(), m => m.Cmd == "-unittestcustom");
    }

    [Fact]
    [DisplayName("命令工厂_未知命令_返回false")]
    public void CommandFactory_UnknownCommand_ReturnsFalse()
    {
        var factory = new CommandFactory(new ServiceBaseTester());

        Assert.False(factory.Handle("-unittestnotexistcmd", null));
        Assert.False(factory.Handle('§', null));
    }
    #endregion

    #region 面板创建工厂
    private sealed class CustomWebPanel(ServiceBase service) : AgentWebPanel(service);

    private class DefaultPanelTester : ServiceBaseTester
    {
        public AgentWebPanel Create() => CreateWebPanel(this);
    }

    private class PanelFactoryTester : ServiceBaseTester
    {
        public Boolean Overridden;

        public AgentWebPanel Create() => CreateWebPanel(this);

        protected override AgentWebPanel CreateWebPanel(ServiceBase service)
        {
            Overridden = true;
            return new CustomWebPanel(service);
        }
    }

    [Fact]
    [DisplayName("CreateWebPanel_默认实现_返回AgentWebPanel")]
    public void CreateWebPanel_Default_ReturnsAgentWebPanel()
    {
        var panel = new DefaultPanelTester().Create();

        Assert.IsType<AgentWebPanel>(panel);
        Assert.NotNull(panel.Service);
    }

    [Fact]
    [DisplayName("CreateWebPanel_子类重写_返回自定义面板")]
    public void CreateWebPanel_Overridden_ReturnsCustomPanel()
    {
        var svc = new PanelFactoryTester();

        var panel = svc.Create();

        Assert.True(svc.Overridden);
        Assert.IsType<CustomWebPanel>(panel);
        Assert.Same(svc, panel.Service);
    }
    #endregion

    #region 嵌入式资源可替换
    private class ResourcePanelTester : AgentWebPanel
    {
        public ResourcePanelTester(ServiceBase service) : base(service) { }

        public String Prefix => EmbeddedResourcePrefix;
        public Type AssemblyType => EmbeddedResourceAssemblyType;
    }

    private class CustomResourcePanel : ResourcePanelTester
    {
        public CustomResourcePanel(ServiceBase service) : base(service) { }

        protected override String EmbeddedResourcePrefix => "UnitTest.WebPanel.wwwroot";
        protected override Type EmbeddedResourceAssemblyType => typeof(CustomResourcePanel);
    }

    [Fact]
    [DisplayName("嵌入式资源_默认_指向Agent内嵌wwwroot")]
    public void EmbeddedResource_Defaults()
    {
        var panel = new ResourcePanelTester(new ServiceBaseTester());

        Assert.Equal("NewLife.Agent.WebPanel.wwwroot", panel.Prefix);
        Assert.Equal(typeof(AgentWebPanel), panel.AssemblyType);
    }

    [Fact]
    [DisplayName("嵌入式资源_子类重写_替换前缀与程序集")]
    public void EmbeddedResource_Override()
    {
        var panel = new CustomResourcePanel(new ServiceBaseTester());

        Assert.Equal("UnitTest.WebPanel.wwwroot", panel.Prefix);
        Assert.Equal(typeof(CustomResourcePanel), panel.AssemblyType);
    }
    #endregion
}
