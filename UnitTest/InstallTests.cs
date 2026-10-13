using Moq;
using NewLife;
using NewLife.Agent;
using NewLife.Agent.CommandHandler;
using NewLife.Agent.Models;

namespace UnitTest;

public class InstallTests
{
    [Fact]
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
