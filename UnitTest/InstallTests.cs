using Moq;
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
        Assert.Equal("-s", model.Arguments);
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
        Assert.Equal("-s", model.Arguments);

        var cur = ".".GetFullPath();
        Assert.Equal(cur, model.WorkingDirectory);

        var exe = cur.CombinePath("testhost.exe");
        var dll = cur.CombinePath("StarAgent.dll");
        Assert.Equal($"{exe} {dll}", model.FileName);
    }
}
