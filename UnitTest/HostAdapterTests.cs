using System.ComponentModel;
using System.Runtime.Versioning;
using NewLife;
using NewLife.Agent;
using NewLife.Agent.Models;

namespace UnitTest;

/// <summary>平台宿主适配单元测试</summary>
/// <remarks>验证各平台宿主可跨平台执行的路径（脚本生成、未安装判定）；需要真实系统服务管理器的安装/启停由集成测试覆盖</remarks>
public class HostAdapterTests
{
    #region SysVinit
    [Fact]
    [DisplayName("SysVinit_未安装的服务_返回未安装")]
    public void SysVinit_NotInstalled_ReturnsFalse()
    {
        const String name = "NewLifeAgentUnitTestNotExist";

        Assert.Null(SysVinit.GetServicePath(name));
        Assert.False(new SysVinit().IsInstalled(name));
    }

    [Fact]
    [DisplayName("SysVinit_构造_名称与安装未实现")]
    public void SysVinit_Install_NotImplemented()
    {
        var host = new SysVinit();
        Assert.Equal("sysVinit", host.Name);

        var ex = Assert.Throws<NotImplementedException>(() => host.Install(new ServiceModel { ServiceName = "x" }));
        Assert.Contains("SysVinit", ex.Message);
    }

    [Fact]
    [DisplayName("SysVinit_无init目录_路径查询不抛异常")]
    public void SysVinit_NoServicePath_NoThrow()
    {
        // 非 Linux（无 /etc/init.d）时，ServicePath 为空，查询应安全返回 null
        if (SysVinit.Available) return;

        Assert.Null(SysVinit.GetServicePath("anything"));
    }
    #endregion

    #region OSXLaunch
    [Fact]
    [DisplayName("OSXLaunch_未安装的服务_返回未安装")]
    public void OSXLaunch_NotInstalled_ReturnsFalse()
    {
        var host = new OSXLaunch();
        Assert.Equal("OSXLaunch", host.Name);

        const String name = "NewLifeAgentUnitTestNotExist";
        Assert.False(host.IsInstalled(name));
        Assert.Null(host.QueryConfig(name));
        Assert.False(host.DependOnNetwork);
    }

    [Fact]
    [DisplayName("OSXLaunch_生成plist_包含标签与启动参数")]
    public void OSXLaunch_BuildPlist_ContainsKeyNodes()
    {
        var plist = OSXLaunch.BuildPlist("MyService", "/usr/local/bin/dotnet", "app.dll -s");

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"UTF-8\"?>", plist);
        Assert.Contains("<key>Label</key>", plist);
        Assert.Contains("<string>MyService</string>", plist);
        Assert.Contains("<string>/usr/local/bin/dotnet</string>", plist);
        Assert.Contains("<string>app.dll -s</string>", plist);
        Assert.Contains("<key>RunAtLoad</key>", plist);
    }

    [Fact]
    [DisplayName("OSXLaunch_生成plist_无参数留空")]
    public void OSXLaunch_BuildPlist_EmptyArgs()
    {
        var plist = OSXLaunch.BuildPlist("svc", "/app/app", "");

        Assert.Contains("<string>/app/app</string>", plist);
        // 空参数替换为空字符串，不残留占位符
        Assert.DoesNotContain("{$Arguments}", plist);
        Assert.DoesNotContain("{$Name}", plist);
        Assert.DoesNotContain("{$FileName}", plist);
    }
    #endregion

    #region Windows 宿主
    [WindowsFact]
    [DisplayName("WindowsService_未安装的服务_返回未安装")]
    public void WindowsService_NotInstalled_ReturnsFalse()
    {
        var host = new WindowsService();
        Assert.False(host.IsInstalled("NewLifeAgentUnitTestNotExist"));
    }

    [WindowsFact]
    [SupportedOSPlatform("windows")]
    [DisplayName("WindowsAutorun_未安装的服务_返回未安装")]
    public void WindowsAutorun_NotInstalled_ReturnsFalse()
    {
        var host = new WindowsAutorun();
        Assert.False(host.IsInstalled("NewLifeAgentUnitTestNotExist"));
        Assert.Null(host.QueryConfig("NewLifeAgentUnitTestNotExist"));
    }
    #endregion
}
