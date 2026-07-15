using System.ComponentModel;
using Moq;
using NewLife.Agent;
using NewLife.Agent.CommandHandler;
using NewLife.Agent.Models;

namespace UnitTest;

/// <summary>命令处理器单元测试</summary>
/// <remarks>验证各命令处理器的 Process 方法行为</remarks>
public class CommandHandlerTests
{
    #region ShowStatus
    [Fact]
    [DisplayName("ShowStatus_构造_正确设置属性")]
    public void ShowStatus_Constructor_SetsProperties()
    {
        var mb = new Mock<ServiceBase> { CallBase = true };

        var handler = new ShowStatus(mb.Object);
        Assert.Equal("-status", handler.Cmd);
        Assert.Equal("显示状态", handler.Description);
    }
    #endregion

    #region Install
    [Fact]
    [DisplayName("Install_Process_CreatesServiceModel")]
    public void Install_Process_CreatesServiceModel()
    {
        ServiceModel model = null;

        var mb1 = new Mock<ServiceBase> { CallBase = true };
        var mb2 = new Mock<DefaultHost> { CallBase = true };

        mb2.Setup(x => x.Install(It.IsAny<ServiceModel>())).Callback<ServiceModel>(m => model = m);

        var svc = mb1.Object;
        svc.Host = mb2.Object;
        svc.ServiceName = "TestService";
        svc.DisplayName = "测试服务";

        var install = new Install(svc);
        install.Process([]);

        Assert.NotNull(model);
        Assert.Equal("TestService", model.ServiceName);
        Assert.Equal("测试服务", model.DisplayName);
    }

    [Fact]
    [DisplayName("Install_Process_带DLL参数_正确设置文件名")]
    public void Install_Process_WithDllArg_SetsFileName()
    {
        ServiceModel model = null;

        var mb1 = new Mock<ServiceBase> { CallBase = true };
        var mb2 = new Mock<DefaultHost> { CallBase = true };

        mb2.Setup(x => x.Install(It.IsAny<ServiceModel>())).Callback<ServiceModel>(m => model = m);

        var svc = mb1.Object;
        svc.Host = mb2.Object;
        svc.ServiceName = "TestSvc";

        var install = new Install(svc);
        install.Process(["MyApp.dll"]);

        Assert.NotNull(model);
        Assert.NotNull(model.FileName);
    }
    #endregion

    #region Start
    [Fact]
    [DisplayName("Start_Process_调用Host.Start")]
    public void Start_Process_CallsHostStart()
    {
        var started = false;

        var mb1 = new Mock<ServiceBase> { CallBase = true };
        var mb2 = new Mock<DefaultHost> { CallBase = true };

        mb2.Setup(x => x.Start(It.IsAny<String>())).Callback<String>(_ => started = true);
        mb2.Setup(x => x.IsRunning(It.IsAny<String>())).Returns(false);
        mb2.Setup(x => x.IsInstalled(It.IsAny<String>())).Returns(true);

        var svc = mb1.Object;
        svc.Host = mb2.Object;
        svc.ServiceName = "TestSvc";

        var handler = new Start(svc);
        var ex = Record.Exception(() => handler.Process([]));

        Assert.Null(ex);
        Assert.True(started);
    }

    [Fact]
    [DisplayName("Start_IsShowMenu_已安装未运行时_显示")]
    public void Start_IsShowMenu_InstalledNotRunning_Shows()
    {
        var mb1 = new Mock<ServiceBase> { CallBase = true };
        var mb2 = new Mock<DefaultHost> { CallBase = true };

        mb2.Setup(x => x.IsInstalled(It.IsAny<String>())).Returns(true);
        mb2.Setup(x => x.IsRunning(It.IsAny<String>())).Returns(false);

        var svc = mb1.Object;
        svc.Host = mb2.Object;
        svc.ServiceName = "TestSvc";

        var handler = new Start(svc);
        Assert.True(handler.IsShowMenu());
    }

    [Fact]
    [DisplayName("Start_IsShowMenu_未安装时_不显示")]
    public void Start_IsShowMenu_NotInstalled_Hides()
    {
        var mb1 = new Mock<ServiceBase> { CallBase = true };
        var mb2 = new Mock<DefaultHost> { CallBase = true };

        mb2.Setup(x => x.IsInstalled(It.IsAny<String>())).Returns(false);

        var svc = mb1.Object;
        svc.Host = mb2.Object;
        svc.ServiceName = "TestSvc";

        var handler = new Start(svc);
        Assert.False(handler.IsShowMenu());
    }
    #endregion

    #region Stop
    [Fact]
    [DisplayName("Stop_Process_调用Host.Stop")]
    public void Stop_Process_CallsHostStop()
    {
        var stopped = false;

        var mb1 = new Mock<ServiceBase> { CallBase = true };
        var mb2 = new Mock<DefaultHost> { CallBase = true };

        mb2.Setup(x => x.Stop(It.IsAny<String>())).Callback<String>(_ => stopped = true);
        mb2.Setup(x => x.IsRunning(It.IsAny<String>())).Returns(true);

        var svc = mb1.Object;
        svc.Host = mb2.Object;
        svc.ServiceName = "TestSvc";

        var handler = new Stop(svc);
        var ex = Record.Exception(() => handler.Process([]));

        Assert.Null(ex);
        Assert.True(stopped);
    }

    [Fact]
    [DisplayName("Stop_IsShowMenu_服务运行时_显示")]
    public void Stop_IsShowMenu_Running_Shows()
    {
        var mb1 = new Mock<ServiceBase> { CallBase = true };
        var mb2 = new Mock<DefaultHost> { CallBase = true };

        mb2.Setup(x => x.IsRunning(It.IsAny<String>())).Returns(true);

        var svc = mb1.Object;
        svc.Host = mb2.Object;

        var handler = new Stop(svc);
        Assert.True(handler.IsShowMenu());
    }
    #endregion

    #region Restart
    [Fact]
    [DisplayName("Restart_Process_调用Host.Restart")]
    public void Restart_Process_CallsHostRestart()
    {
        var restarted = false;

        var mb1 = new Mock<ServiceBase> { CallBase = true };
        var mb2 = new Mock<DefaultHost> { CallBase = true };

        mb2.Setup(x => x.Restart(It.IsAny<String>())).Callback<String>(_ => restarted = true);
        mb2.Setup(x => x.IsRunning(It.IsAny<String>())).Returns(true);

        var svc = mb1.Object;
        svc.Host = mb2.Object;
        svc.ServiceName = "TestSvc";

        var handler = new Restart(svc);
        var ex = Record.Exception(() => handler.Process([]));

        Assert.Null(ex);
        Assert.True(restarted);
    }
    #endregion

    #region WatchDog
    [Fact]
    [DisplayName("WatchDog_Process_无保护服务_不抛异常")]
    public void WatchDog_Process_NoWatchDogs_NoException()
    {
        var mb = new Mock<ServiceBase> { CallBase = true };

        var handler = new WatchDog(mb.Object);
        var ex = Record.Exception(() => handler.Process([]));

        Assert.Null(ex);
    }

    [Fact]
    [DisplayName("WatchDog_IsShowMenu_无保护服务_不显示")]
    public void WatchDog_IsShowMenu_NoDogs_Hides()
    {
        var mb = new Mock<ServiceBase> { CallBase = true };

        var handler = new WatchDog(mb.Object);
        Assert.False(handler.IsShowMenu());
    }
    #endregion

    #region Reinstall
    [Fact]
    [DisplayName("Reinstall_Process_调用Host停止卸载安装启动")]
    public void Reinstall_Process_CallsHostCycle()
    {
        var hostMb = new Mock<DefaultHost> { CallBase = true };
        hostMb.Setup(x => x.Stop(It.IsAny<String>())).Returns(true);
        hostMb.Setup(x => x.Remove(It.IsAny<String>())).Returns(true);
        hostMb.Setup(x => x.Install(It.IsAny<ServiceModel>())).Returns(true);
        hostMb.Setup(x => x.Start(It.IsAny<String>())).Returns(true);

        var svcMb = new Mock<ServiceBase> { CallBase = true };
        svcMb.Object.Host = hostMb.Object;
        svcMb.Object.ServiceName = "TestSvc";

        var handler = new Reinstall(svcMb.Object);
        var ex = Record.Exception(() => handler.Process([]));

        Assert.Null(ex);
        hostMb.Verify(x => x.Stop("TestSvc"), Times.Once);
        hostMb.Verify(x => x.Remove("TestSvc"), Times.Once);
        hostMb.Verify(x => x.Install(It.IsAny<ServiceModel>()), Times.Once);
        hostMb.Verify(x => x.Start("TestSvc"), Times.Once);
    }
    #endregion

    #region Remove
    [Fact]
    [DisplayName("Remove_Process_调用Host.Remove")]
    public void Remove_Process_CallsHostRemove()
    {
        var removed = false;

        var hostMb = new Mock<DefaultHost> { CallBase = true };
        hostMb.Setup(x => x.Remove(It.IsAny<String>())).Callback<String>(_ => removed = true);
        hostMb.Setup(x => x.IsRunning(It.IsAny<String>())).Returns(false);
        hostMb.Setup(x => x.Stop(It.IsAny<String>())).Returns(true);

        var svcMb = new Mock<ServiceBase> { CallBase = true };
        svcMb.Object.Host = hostMb.Object;
        svcMb.Object.ServiceName = "TestSvc";

        var handler = new Remove(svcMb.Object);
        var ex = Record.Exception(() => handler.Process([]));

        Assert.Null(ex);
        Assert.True(removed);
    }

    [Fact]
    [DisplayName("Remove_IsShowMenu_服务已安装_显示")]
    public void Remove_IsShowMenu_Installed_Shows()
    {
        var hostMb = new Mock<DefaultHost> { CallBase = true };
        hostMb.Setup(x => x.IsInstalled(It.IsAny<String>())).Returns(true);

        var svcMb = new Mock<ServiceBase> { CallBase = true };
        svcMb.Object.Host = hostMb.Object;

        var handler = new Remove(svcMb.Object);
        Assert.True(handler.IsShowMenu());
    }
    #endregion

    #region Uninstall
    [Fact]
    [DisplayName("Uninstall_Process_先停止再卸载")]
    public void Uninstall_Process_StopsThenRemoves()
    {
        var steps = new List<String>();

        var hostMb = new Mock<DefaultHost> { CallBase = true };
        hostMb.Setup(x => x.Stop(It.IsAny<String>())).Callback<String>(_ => steps.Add("stop")).Returns(true);
        hostMb.Setup(x => x.Remove(It.IsAny<String>())).Callback<String>(_ => steps.Add("remove")).Returns(true);

        var svcMb = new Mock<ServiceBase> { CallBase = true };
        svcMb.Object.Host = hostMb.Object;
        svcMb.Object.ServiceName = "TestSvc";

        var handler = new Uninstall(svcMb.Object);
        var ex = Record.Exception(() => handler.Process([]));

        Assert.Null(ex);
        Assert.Equal(2, steps.Count);
        Assert.Equal("stop", steps[0]);
        Assert.Equal("remove", steps[1]);
    }
    #endregion

    #region RunSimulation
    [Fact]
    [DisplayName("RunSimulation_Cmd_等于-run")]
    public void RunSimulation_Cmd_IsRun()
    {
        var mb = new Mock<ServiceBase> { CallBase = true };

        var handler = new RunSimulation(mb.Object);
        Assert.Equal("-run", handler.Cmd);
        Assert.Equal("模拟运行", handler.Description);
    }
    #endregion

    #region RunService
    [Fact]
    [DisplayName("RunService_Cmd_等于-s")]
    public void RunService_Cmd_IsS()
    {
        var mb = new Mock<ServiceBase> { CallBase = true };

        var handler = new RunService(mb.Object);
        Assert.Equal("-s", handler.Cmd);
        Assert.Equal("执行服务", handler.Description);
        Assert.Null(handler.ShortcutKey);
    }

    [Fact]
    [DisplayName("RunService_Process_调用Host.Run")]
    public void RunService_Process_CallsHostRun()
    {
        var called = false;

        var svcMb = new Mock<ServiceBase> { CallBase = true };
        var hostMb = new Mock<DefaultHost> { CallBase = true };

        hostMb.Setup(x => x.Run(It.IsAny<ServiceBase>())).Callback<ServiceBase>(_ => called = true);
        svcMb.Object.Host = hostMb.Object;

        var handler = new RunService(svcMb.Object);
        var ex = Record.Exception(() => handler.Process([]));

        Assert.Null(ex);
        Assert.True(called);
    }
    #endregion

    #region CommandFactory
    [Fact]
    [DisplayName("CommandFactory_构造_不抛异常")]
    public void CommandFactory_Constructor_NoException()
    {
        var mb = new Mock<ServiceBase> { CallBase = true };

        // CommandFactory 在 ServiceBase 构造函数中已创建
        Assert.NotNull(mb.Object.Command);
    }

    [Fact]
    [DisplayName("CommandFactory_Handle_未知命令_返回false")]
    public void CommandFactory_Handle_UnknownCmd_ReturnsFalse()
    {
        var mb = new Mock<ServiceBase> { CallBase = true };

        var result = mb.Object.Command.Handle("-unknown", []);
        Assert.False(result);
    }

    [Fact]
    [DisplayName("CommandFactory_Handle_按键_存在命令_返回true")]
    public void CommandFactory_Handle_Key_Found_ReturnsTrue()
    {
        var mb = new Mock<ServiceBase> { CallBase = true };
        mb.Object.ServiceName = "TestSvc";
        mb.Object.Host = new Mock<DefaultHost> { CallBase = true }.Object;

        // 快捷键 '1' 对应 ShowStatus（始终显示）
        var result = mb.Object.Command.Handle('1', []);
        Assert.True(result);
    }

    [Fact]
    [DisplayName("CommandFactory_Handle_按键_无对应命令_返回false")]
    public void CommandFactory_Handle_Key_NotFound_ReturnsFalse()
    {
        var mb = new Mock<ServiceBase> { CallBase = true };

        // 快捷键 '9' 没有对应命令
        var result = mb.Object.Command.Handle('9', []);
        Assert.False(result);
    }

    [Fact]
    [DisplayName("CommandFactory_GetShortcutMenu_返回有序菜单")]
    public void CommandFactory_GetShortcutMenu_ReturnsSortedMenus()
    {
        var mb = new Mock<ServiceBase> { CallBase = true };
        mb.Object.ServiceName = "TestSvc";
        mb.Object.Host = new Mock<DefaultHost> { CallBase = true }.Object;

        var menus = mb.Object.Command.GetShortcutMenu();
        Assert.NotNull(menus);
        Assert.True(menus.Count > 0);
    }

    [Fact]
    [DisplayName("CommandFactory_GetAllCommand_返回所有命令")]
    public void CommandFactory_GetAllCommand_ReturnsAll()
    {
        var mb = new Mock<ServiceBase> { CallBase = true };

        var all = mb.Object.Command.GetAllCommand();
        Assert.NotNull(all);
        Assert.True(all.Count > 0);
    }
    #endregion
}
