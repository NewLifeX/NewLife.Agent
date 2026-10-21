using System.ComponentModel;
using System.Globalization;
using Moq;
using NewLife;
using NewLife.Agent;
using NewLife.Agent.CommandHandler;
using NewLife.Agent.Models;

namespace UnitTest;

public class InstallTests
{
    [Fact]
    [DisplayName("安装服务_无额外参数_生成服务模型并调用宿主安装")]
    public void InstallExe()
    {
        ServiceModel model = null;

        var mb1 = new Mock<ServiceBase> { CallBase = true };
        var mb2 = new Mock<DefaultHost> { CallBase = true };

        mb2.Setup(x => x.Install(It.IsAny<ServiceModel>())).Callback<ServiceModel>(m => model = m);

        var svc = mb1.Object;
        var host = mb2.Object;
        svc.Host = host;

        svc.ServiceName = "StarAgent";
        svc.DisplayName = "星尘代理";

        var install = new Install(svc);
        install.Process([]);

        Assert.NotNull(model);
        Assert.Equal(svc.ServiceName, model.ServiceName);
        Assert.Equal(svc.DisplayName, model.DisplayName);
        Assert.StartsWith("-s", model.Arguments);
    }

    [Fact]
    [DisplayName("安装服务_显示名与描述按当前语言翻译")]
    public void Install_TranslatesDisplayMetadata()
    {
        ServiceModel model = null;

        var mb1 = new Mock<ServiceBase> { CallBase = true };
        var mb2 = new Mock<DefaultHost> { CallBase = true };
        mb2.Setup(x => x.Install(It.IsAny<ServiceModel>())).Callback<ServiceModel>(m => model = m);

        var svc = mb1.Object;
        svc.Host = mb2.Object;
        svc.ServiceName = "StarAgent";
        svc.DisplayName = "Cross-platform Service Agent";
        svc.Description = "Cross-platform system service and daemon management framework with install, monitoring, and auto-restart support.";

        var prevCulture = Localization.Culture;
        try
        {
            Localization.Culture = CultureInfo.GetCultureInfo("zh-CN");
            new Install(svc).Process([]);
        }
        finally
        {
            Localization.Culture = prevCulture;
        }

        Assert.NotNull(model);
        Assert.Equal("跨平台服务守护", model.DisplayName);
        Assert.Equal("跨平台系统服务与守护进程管理框架，支持安装、监控与自动重启。", model.Description);
        // 原始配置值保持不变
        Assert.Equal("Cross-platform Service Agent", svc.DisplayName);
        Assert.StartsWith("Cross-platform system service", svc.Description);
    }

    [Fact]
    [DisplayName("安装服务_指定Dll参数_拼接运行时与目标程序路径")]
    public void InstallDll()
    {
        ServiceModel model = null;

        var mb1 = new Mock<ServiceBase> { CallBase = true };
        var mb2 = new Mock<DefaultHost> { CallBase = true };

        mb2.Setup(x => x.Install(It.IsAny<ServiceModel>())).Callback<ServiceModel>(m => model = m);

        var svc = mb1.Object;
        var host = mb2.Object;
        svc.Host = host;

        svc.ServiceName = "StarAgent";
        svc.DisplayName = "星尘代理";

        var install = new Install(svc);
        install.Process(["StarAgent.dll"]);

        Assert.NotNull(model);
        Assert.Equal(svc.ServiceName, model.ServiceName);
        Assert.Equal(svc.DisplayName, model.DisplayName);
        Assert.StartsWith("-s", model.Arguments);

        var cur = ".".GetFullPath();
        Assert.Equal(cur, model.WorkingDirectory);

        // Windows 下测试宿主为 testhost.exe；Linux 下为 dotnet 运行时主程序（muxer）。二者都会被识别为运行时主程序并追加主 dll
        var exe = Runtime.Windows
            ? cur.CombinePath("testhost.exe")
            : System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
        //var dll = cur.CombinePath("StarAgent.dll");
        var dll = cur.CombinePath("testhost.dll");
        Assert.Equal($"{exe} {dll}", model.FileName);
    }
}
