using System.ComponentModel;
using System.Diagnostics;
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
    [Theory]
    [InlineData("agent is running", true)]
    [InlineData("agent is not running", false)]
    [InlineData("not running", false)]
    [InlineData("", false)]
    [InlineData("Starting...\nagent is running", true)]
    [DisplayName("SysVinit_状态输出逐行判定_not running 不算运行")]
    public void SysVinit_IsRunningStatus_ParsesLines(String status, Boolean expected)
        => Assert.Equal(expected, SysVinit.IsRunningStatus(status));
    [Fact]
    [DisplayName("SysVinit_未安装的服务_返回未安装")]
    public void SysVinit_NotInstalled_ReturnsFalse()
    {
        const String name = "NewLifeAgentUnitTestNotExist";

        Assert.Null(SysVinit.GetServicePath(name));
        Assert.False(new SysVinit().IsInstalled(name));
    }

    [Fact]
    [DisplayName("SysVinit_未安装的服务_卸载与状态查询返回false")]
    public void SysVinit_NotInstalled_RemoveAndRunning_False()
    {
        const String name = "NewLifeAgentUnitTestNotExist";
        var host = new SysVinit();

        // 未安装时不应尝试执行 update-rc.d 或 kill，直接返回 false
        Assert.False(host.IsRunning(name));
        Assert.False(host.Remove(name));
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
        // launchd 按 argv 逐项传参，必须拆成独立 <string>
        Assert.Contains("<string>app.dll</string>", plist);
        Assert.Contains("<string>-s</string>", plist);
        Assert.DoesNotContain("<string>app.dll -s</string>", plist);
        Assert.Contains("<key>RunAtLoad</key>", plist);
    }

    [Fact]
    [DisplayName("OSXLaunch_生成plist_多参数逐项拆分")]
    public void OSXLaunch_BuildPlist_SplitsArguments()
    {
        var plist = OSXLaunch.BuildPlist("svc", "/app/app", "-s --urls \"http://0.0.0.0:80\"");

        Assert.Contains("<string>-s</string>", plist);
        Assert.Contains("<string>--urls</string>", plist);
        Assert.Contains("<string>http://0.0.0.0:80</string>", plist);
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

    [Fact]
    [DisplayName("OSXLaunch_生成plist_复合命令行拆成独立argv")]
    public void OSXLaunch_BuildPlist_SplitsCompositeFileName()
    {
        var plist = OSXLaunch.BuildPlist("svc", "dotnet \"my app.dll\"", "-s");

        Assert.Contains("<string>dotnet</string>", plist);
        Assert.Contains("<string>my app.dll</string>", plist);
        Assert.DoesNotContain("<string>dotnet \"my app.dll\"</string>", plist);
        Assert.Contains("<string>-s</string>", plist);
    }

    [Fact]
    [DisplayName("OSXLaunch_生成plist_单个路径含空格整体保留")]
    public void OSXLaunch_BuildPlist_KeepsSinglePathWithSpaces()
    {
        var plist = OSXLaunch.BuildPlist("svc", "/opt/my app/agent", "");

        Assert.Contains("<string>/opt/my app/agent</string>", plist);
        Assert.DoesNotContain("<string>/opt/my</string>", plist);
    }

    [Fact]
    [DisplayName("OSXLaunch_生成plist_特殊字符做XML转义")]
    public void OSXLaunch_BuildPlist_EscapesXml()
    {
        var plist = OSXLaunch.BuildPlist("svc", "/app/a&b", "\"x < y\"", "user1", true);

        Assert.Contains("/app/a&amp;b", plist);
        Assert.Contains("x &lt; y", plist);
        Assert.Contains("<key>UserName</key>", plist);
        Assert.Contains("<string>user1</string>", plist);
        Assert.Contains("<key>NetworkState</key>", plist);
    }

    [Fact]
    [DisplayName("OSXLaunch_生成plist_无参数不留空 string")]
    public void OSXLaunch_BuildPlist_NoEmptyArgument()
    {
        var plist = OSXLaunch.BuildPlist("svc", "/app/app", "");

        // 空参数不输出空 <string>，否则 launchd 会给进程传一个空参数
        Assert.DoesNotContain(plist.Split('\n'), e => e.Trim() == "<string></string>");
    }

    [Fact]
    [DisplayName("陈旧PID判定_进程启动晚于pid文件视为已失效")]
    public void IsStalePid_ProcessStartedAfterPidFile_Stale()
    {
        using var p = Process.GetCurrentProcess();

        // pid 文件写入时间早于进程启动时间，说明 PID 已被其它进程复用
        Assert.True(ServiceHelper.IsStalePid(p, DateTime.Now.AddMinutes(-5)));
        // 正常情况：pid 文件在进程启动之后写入
        Assert.False(ServiceHelper.IsStalePid(p, DateTime.Now.AddMinutes(5)));
    }

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
