using System.ComponentModel;
using NewLife.Agent;

namespace UnitTest;

/// <summary>ServiceBase 测试子类，暴露 protected 方法供测试</summary>
public class ServiceBaseTester : ServiceBase
{
    public new Boolean CheckMemory() => base.CheckMemory();
    public new Boolean CheckThread() => base.CheckThread();
    public new Boolean CheckHandle() => base.CheckHandle();
    public new Boolean CheckAutoRestart() => base.CheckAutoRestart();
    public new void DoCheck() => base.DoCheck(null);
}

/// <summary>ServiceBase 核心逻辑单元测试</summary>
/// <remarks>验证健康检查方法的行为</remarks>
public class ServiceBaseTests
{
    [Fact]
    [DisplayName("CheckMemory_未超阈值_不触发重启")]
    public void CheckMemory_BelowThreshold_NoRestart()
    {
        var svc = new ServiceBaseTester();

        // MaxMemory 默认为 0（不限），不会触发重启
        var result = svc.CheckMemory();

        Assert.False(result);
    }

    [Fact]
    [DisplayName("CheckThread_未超阈值_不触发重启")]
    public void CheckThread_BelowThreshold_NoRestart()
    {
        var svc = new ServiceBaseTester();

        // MaxThread 默认 1000，当前进程通常远低于此
        var result = svc.CheckThread();

        Assert.False(result);
    }

    [Fact]
    [DisplayName("CheckHandle_未超阈值_不触发重启")]
    public void CheckHandle_BelowThreshold_NoRestart()
    {
        var svc = new ServiceBaseTester();

        // MaxHandle 默认 10000，当前进程通常远低于此
        var result = svc.CheckHandle();

        Assert.False(result);
    }

    [Fact]
    [DisplayName("CheckAutoRestart_AutoRestart为零_不触发")]
    public void CheckAutoRestart_Disabled_NoRestart()
    {
        var svc = new ServiceBaseTester();

        var result = svc.CheckAutoRestart();

        Assert.False(result);
    }

    [Fact]
    [DisplayName("FreeMemory_调用后不抛异常")]
    public void FreeMemory_NoException()
    {
        var svc = new ServiceBaseTester();

        // FreeMemory 不应抛出异常
        var ex = Record.Exception(() => svc.FreeMemory());
        Assert.Null(ex);
    }

    [Fact]
    [DisplayName("DoCheck_异常被吞没_不向外传播")]
    public void DoCheck_ExceptionSwallowed_NotPropagated()
    {
        var svc = new ServiceBaseTester();

        // DoCheck 内部应捕获所有异常，不向外传播
        var ex = Record.Exception(() => svc.DoCheck());
        Assert.Null(ex);
    }

    [Fact]
    [DisplayName("ServiceName_默认空_可设置")]
    public void ServiceName_CanBeSet()
    {
        var svc = new ServiceBaseTester();

        svc.ServiceName = "TestService";
        Assert.Equal("TestService", svc.ServiceName);
    }

    [Fact]
    [DisplayName("DisplayName_默认空_可设置")]
    public void DisplayName_CanBeSet()
    {
        var svc = new ServiceBaseTester();

        svc.DisplayName = "测试服务";
        Assert.Equal("测试服务", svc.DisplayName);
    }

    [Fact]
    [DisplayName("Running_初始为false_可设置")]
    public void Running_InitialFalse()
    {
        var svc = new ServiceBaseTester();

        Assert.False(svc.Running);

        svc.Running = true;
        Assert.True(svc.Running);
    }
}
